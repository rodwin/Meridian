using Infrastructure.Database;
using Infrastructure.Outbox;
using Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using Worker.Messaging;

namespace Worker;

// Relays pending OutboxMessages to the job queue (Service Bus in production,
// in-memory in development). Runs as a background loop, processing all active
// tenants in parallel on each tick.
//
// The outbox pattern guarantees that domain events and scheduled triggers are
// never lost: the API saves a message to the outbox in the same DB transaction
// as the business change, so both succeed or both fail together. This service
// is responsible for forwarding those saved messages onwards to the job queue.
//
// Two-phase claim design
// ─────────────────────
// The naive approach — read rows, call Service Bus, commit a single transaction
// — is dangerous because it holds a SQL transaction open across a network call:
//
//   If Service Bus succeeds but the DB commit fails → the message is retried
//   → duplicate delivery (ASB MessageId dedup is the only safeguard, and it
//     relies on duplicate detection being enabled on the queue).
//
//   If the DB holds UPDLOCK for the full Service Bus round-trip → unnecessary
//   lock contention under load.
//
// Instead we use three distinct phases with no transaction open during I/O:
//
//   Phase 1 — Claim   : mark rows ProcessingStartedAt = NOW inside a short
//                        transaction, then commit immediately.
//   Phase 2 — Relay   : publish to the job queue outside any transaction.
//   Phase 3 — Persist : write IsRelayed / Error back (no transaction needed).
//
// Reaper
// ──────
// A worker crash between Phase 1 and Phase 3 leaves rows with ProcessingStartedAt
// set but IsRelayed = false — permanently stuck without intervention.
// The reaper resets those rows every ReaperInterval so the next poll picks them up.
// ASB's MessageId-based duplicate detection handles the rare case where the crash
// occurred after a successful publish but before Phase 3 persisted IsRelayed = true.
internal sealed class OutboxProcessorService(
    ITenantRegistry tenantRegistry,
    IJobQueue jobQueue,
    IOptions<WorkerOptions> options,
    ILogger<OutboxProcessorService> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ReaperInterval = TimeSpan.FromMinutes(5);

    private const int BatchSize = 20;

    // Selects unclaimed, un-relayed messages ordered by age (oldest first).
    // UPDLOCK prevents two concurrent workers from reading the same rows.
    // READPAST skips rows already locked by another connection rather than
    // blocking — essential for parallel tenant processing within one pod.
    // ProcessingStartedAt IS NULL excludes rows already claimed by this or
    // another worker instance that hasn't finished yet.
    private const string ClaimBatchSql = """
        SELECT TOP ({0}) Id, MessageType, QueueType, TraceParent, Type, Payload,
                         OccurredOnUtc, IsRelayed, RelayedAt, Error, ProcessingStartedAt
        FROM outbox.OutboxMessages WITH (UPDLOCK, READPAST)
        WHERE IsRelayed = 0
          AND ProcessingStartedAt IS NULL
        ORDER BY OccurredOnUtc
        """;

    // Resets rows that were claimed (Phase 1 committed) but never marked as
    // relayed (Phase 3 never ran), typically because the worker crashed.
    // 5 minutes is generous enough to avoid resetting rows from a slow-but-alive
    // worker while still recovering quickly after a crash.
    private const string ResetStaleClaimsSql = """
        UPDATE outbox.OutboxMessages
        SET ProcessingStartedAt = NULL
        WHERE IsRelayed = 0
          AND ProcessingStartedAt < DATEADD(minute, -5, GETUTCDATE())
        """;

    private DateTime _lastReaperRunUtc = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            IReadOnlyList<TenantInfo> tenants = await tenantRegistry.GetAllAsync();

            if (tenants.Count == 0)
            {
                logger.LogDebug("Outbox processor: no active tenants found, skipping poll");
            }
            else
            {
                // Compute once per tick so every tenant gets the same reaper decision,
                // then update the timestamp after all parallel work completes.
                bool runReaper = DateTime.UtcNow - _lastReaperRunUtc >= ReaperInterval;

                // IO-bound work — use a concurrency limit well above CPU count.
                // Without explicit ParallelOptions the default is Environment.ProcessorCount,
                // which in a containerised pod with limited CPU would serialise tenant processing.
                await Parallel.ForEachAsync(
                    tenants,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = options.Value.MaxConcurrentTenants,
                        CancellationToken = stoppingToken
                    },
                    async (tenant, ct) => await ProcessTenantOutboxAsync(tenant, runReaper, ct));

                if (runReaper)
                {
                    _lastReaperRunUtc = DateTime.UtcNow;
                }
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    internal async Task ProcessTenantOutboxAsync(
        TenantInfo tenant,
        bool runReaper,
        CancellationToken cancellationToken)
    {
        try
        {
            DbContextOptions<ApplicationDbContext> dbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(tenant.ConnectionString, sqlOptions =>
                    sqlOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.App))
                .Options;

            await using ApplicationDbContext context = new(dbContextOptions);

            // Run the reaper before claiming new messages so that any rows stuck
            // from a previous crash are available for this poll cycle to pick up.
            if (runReaper)
            {
                await ResetStaleClaimsAsync(context, tenant.TenantId, cancellationToken);
            }

            // ── Phase 1: Claim ──────────────────────────────────────────────────────
            // Read pending messages inside a short transaction and immediately mark
            // them as "in-progress" by setting ProcessingStartedAt. Committing here
            // releases the UPDLOCK before any Service Bus call is made, keeping lock
            // duration to a minimum (row read + one UPDATE — no network I/O).
            List<OutboxMessage> messages;
            await using (var transaction = await context.Database.BeginTransactionAsync(cancellationToken))
            {
                messages = await context.OutboxMessages
                    .FromSqlRaw(ClaimBatchSql, BatchSize)
                    .ToListAsync(cancellationToken);

                if (messages.Count == 0)
                {
                    logger.LogDebug("Outbox processor: no pending messages for tenant {TenantId}", tenant.TenantId);
                    return;
                }

                foreach (OutboxMessage message in messages)
                {
                    message.ProcessingStartedAt = DateTime.UtcNow;
                }

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken); // locks released here
            }

            // ── Phase 2: Relay ──────────────────────────────────────────────────────
            // Publish each claimed message to the job queue with no transaction open.
            // On failure, ProcessingStartedAt is cleared immediately so the next poll
            // retries without waiting for the reaper timeout.
            foreach (OutboxMessage message in messages)
            {
                try
                {
                    await jobQueue.EnqueueAsync(
                        tenant.TenantId,
                        new JobMessage
                        {
                            MessageType = message.MessageType,
                            QueueType = message.QueueType,
                            EventType = message.Type,
                            Payload = message.Payload,
                            IdempotencyKey = message.Id.ToString(), // ASB MessageId dedup
                            TraceParent = message.TraceParent
                        },
                        cancellationToken);

                    message.IsRelayed = true;
                    message.RelayedAt = DateTime.UtcNow;
                    message.ProcessingStartedAt = null;
                    message.Error = null;
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Failed to enqueue outbox message {Id} for tenant {TenantId} — will retry on next poll",
                        message.Id,
                        tenant.TenantId);

                    // Clear the claim immediately so the next poll retries this message
                    // rather than waiting for the 5-minute reaper window.
                    message.ProcessingStartedAt = null;
                    message.Error = ex.ToString();
                }
            }

            // ── Phase 3: Persist relay outcomes ─────────────────────────────────────
            // Write IsRelayed, RelayedAt, ProcessingStartedAt, and Error back to the DB.
            // No transaction needed — these are idempotent status updates.
            await context.SaveChangesAsync(cancellationToken);

            int relayed = messages.Count(m => m.IsRelayed);

            logger.LogInformation(
                "Processed {Total} outbox messages for tenant {TenantId}: {Relayed} relayed, {Failed} failed",
                messages.Count,
                tenant.TenantId,
                relayed,
                messages.Count - relayed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process outbox batch for tenant {TenantId}", tenant.TenantId);
        }
    }

    // Resets rows that were claimed but never relayed, recovering from worker crashes.
    // Logs a warning with the count so ops can detect unusually high crash rates
    // (e.g. a misconfigured Service Bus connection string causing constant failures).
    private async Task ResetStaleClaimsAsync(
        ApplicationDbContext context,
        string tenantId,
        CancellationToken cancellationToken)
    {
        int reset = await context.Database.ExecuteSqlRawAsync(ResetStaleClaimsSql, cancellationToken);

        if (reset > 0)
        {
            logger.LogWarning(
                "Reaper reset {Count} stale outbox claims for tenant {TenantId} " +
                "(stuck >5 min — indicates a previous worker crash between claim and relay)",
                reset,
                tenantId);
        }
    }
}
