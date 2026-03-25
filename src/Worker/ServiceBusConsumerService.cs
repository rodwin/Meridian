using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Infrastructure.Tenancy;
using Microsoft.Extensions.Options;
using Worker.Messaging;

namespace Worker;

// Manages one ServiceBusProcessor per tenant per queue type.
//
// Dynamic tenant reconciliation
// ──────────────────────────────
// Tenants are not static — they can be added or deactivated at runtime without
// restarting the worker. A one-time startup load would leave new tenants without
// processors and keep processors running for deactivated tenants indefinitely.
//
// To handle this, ReconcileProcessorsAsync runs on startup and then every
// ReconciliationInterval. On each tick it diffs the live tenant list against the
// set of currently tracked processors:
//
//   New tenant      → provision ASB queues (if missing) + start processors
//   Removed tenant  → stop and dispose processors
//   Unchanged       → no-op
//
// The reconciliation loop replaces the original Task.Delay(Infinite) hold pattern.
// Queue provisioning for new tenants is handled inline here; ServiceBusProvisioningService
// still handles the bulk initial provisioning at startup before this service starts.
internal sealed class ServiceBusConsumerService(
    ServiceBusClient client,
    ServiceBusAdministrationClient adminClient,
    ITenantRegistry tenantRegistry,
    MessageDispatcher dispatcher,
    IOptions<WorkerOptions> options,
    ILogger<ServiceBusConsumerService> logger) : BackgroundService
{
    // Tracks active processors keyed by tenantId so we can diff against the
    // live tenant list on each reconciliation tick.
    private readonly Dictionary<string, List<ServiceBusProcessor>> _processorsByTenant = [];

    // Named to match the AddSource registration in ServiceDefaults so spans are
    // exported. The SDK's "Azure.Messaging.ServiceBus" source covers the raw
    // transport (receive/complete/abandon). This source adds a child span with
    // tenant and message-type context that the SDK span alone doesn't carry.
    private static readonly ActivitySource ActivitySource = new("Worker.ServiceBusConsumer");

    private static readonly TimeSpan ReconciliationInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Perform an immediate reconciliation on startup so processors are ready
        // before the first timer tick.
        await ReconcileProcessorsAsync(stoppingToken);

        using PeriodicTimer timer = new(ReconciliationInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ReconcileProcessorsAsync(stoppingToken);
            }
        }
        finally
        {
            // Graceful shutdown — stop all processors regardless of how the loop exited.
            // Use CancellationToken.None so in-flight messages can finish settling
            // rather than being abandoned mid-processing.
            foreach (ServiceBusProcessor processor in _processorsByTenant.Values.SelectMany(p => p))
            {
                await processor.StopProcessingAsync(CancellationToken.None);
                await processor.DisposeAsync();
            }
        }
    }

    // Diffs the live tenant registry against currently tracked processors and
    // starts or stops processors to match the current state.
    private async Task ReconcileProcessorsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<TenantInfo> currentTenants = await tenantRegistry.GetAllAsync();
        HashSet<string> currentTenantIds = currentTenants.Select(t => t.TenantId).ToHashSet();

        // Start processors for tenants that appeared since the last reconciliation.
        foreach (TenantInfo tenant in currentTenants)
        {
            if (_processorsByTenant.ContainsKey(tenant.TenantId))
            {
                continue;
            }

            // Provision queues before starting processors — the processor will fail
            // immediately if the queue doesn't exist. ServiceBusProvisioningService
            // handles existing tenants at startup; this covers tenants added at runtime.
            await EnsureQueuesExistAsync(tenant.TenantId, cancellationToken);
            await StartTenantProcessorsAsync(tenant.TenantId, cancellationToken);
        }

        // Stop processors for tenants that are no longer active.
        List<string> removedTenantIds = _processorsByTenant.Keys.Except(currentTenantIds).ToList();

        foreach (string tenantId in removedTenantIds)
        {
            await StopTenantProcessorsAsync(tenantId);
        }
    }

    private async Task EnsureQueuesExistAsync(string tenantId, CancellationToken cancellationToken)
    {
        foreach (QueueTypeConfig queue in options.Value.Queues)
        {
            string queueName = $"{tenantId}-{queue.Type}";

            if (!await adminClient.QueueExistsAsync(queueName, cancellationToken))
            {
                await adminClient.CreateQueueAsync(TenantQueueFactory.BuildOptions(queueName), cancellationToken);

                logger.LogInformation("Created ASB queue '{QueueName}' for new tenant {TenantId}", queueName, tenantId);
            }
        }
    }

    private async Task StartTenantProcessorsAsync(string tenantId, CancellationToken cancellationToken)
    {
        var processors = new List<ServiceBusProcessor>();

        foreach (QueueTypeConfig queueConfig in options.Value.Queues)
        {
            ServiceBusProcessor processor = client.CreateProcessor(
                $"{tenantId}-{queueConfig.Type}",
                new ServiceBusProcessorOptions
                {
                    MaxConcurrentCalls = queueConfig.MaxConcurrentCalls,
                    AutoCompleteMessages = false,
                    PrefetchCount = 5
                });

            processor.ProcessMessageAsync += args => HandleMessageAsync(tenantId, args, cancellationToken);
            processor.ProcessErrorAsync += args => HandleErrorAsync(tenantId, queueConfig.Type, args);

            await processor.StartProcessingAsync(cancellationToken);
            processors.Add(processor);

            logger.LogInformation(
                "Started ASB processor for tenant {TenantId} queue '{QueueType}' (maxConcurrent={Max})",
                tenantId, queueConfig.Type, queueConfig.MaxConcurrentCalls);
        }

        _processorsByTenant[tenantId] = processors;
    }

    private async Task StopTenantProcessorsAsync(string tenantId)
    {
        if (!_processorsByTenant.Remove(tenantId, out List<ServiceBusProcessor>? processors))
        {
            return;
        }

        foreach (ServiceBusProcessor processor in processors)
        {
            await processor.StopProcessingAsync(CancellationToken.None);
            await processor.DisposeAsync();
        }

        logger.LogInformation("Stopped ASB processors for deactivated tenant {TenantId}", tenantId);
    }

    private async Task HandleMessageAsync(
        string tenantId,
        ProcessMessageEventArgs args,
        CancellationToken cancellationToken)
    {
        JobMessage? message = args.Message.Body.ToObjectFromJson<JobMessage>();

        // The outbox pattern deliberately breaks synchronous trace propagation:
        // the relay sends messages in a background loop, so Activity.Current at send
        // time is the outbox processor span — not the original API request.
        // The ASB SDK therefore starts a new root trace at the consumer.
        //
        // TraceParent is captured from Activity.Current when the outbox message is
        // written (inside the API request), so it holds the original trace context.
        // Restoring it here re-attaches the consume span to the originating request,
        // making the full API → outbox → ASB → handler chain visible in one trace.
        ActivityContext parentContext = default;
        if (message?.TraceParent is not null)
        {
            ActivityContext.TryParse(message.TraceParent, null, isRemote: true, out parentContext);
        }

        using Activity? activity = ActivitySource.StartActivity(
            $"consume {message?.MessageType ?? "unknown"}",
            ActivityKind.Consumer,
            parentContext: parentContext);

        activity?.SetTag("messaging.system", "servicebus");
        activity?.SetTag("messaging.tenant_id", tenantId);
        activity?.SetTag("messaging.message_id", args.Message.MessageId);
        activity?.SetTag("messaging.message_type", message?.MessageType);
        activity?.SetTag("messaging.event_type", message?.EventType);

        try
        {
            await dispatcher.DispatchAsync(tenantId, message!, cancellationToken);
            await args.CompleteMessageAsync(args.Message, cancellationToken);
        }
        catch (Exception ex)
        {
            // Mark the span as failed so it surfaces in error views in Aspire/Jaeger/etc.
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            logger.LogError(
                ex,
                "Failed to process message {MessageId} for tenant {TenantId}",
                args.Message.MessageId,
                tenantId);

            await args.AbandonMessageAsync(args.Message, cancellationToken: cancellationToken);
        }
    }

    private Task HandleErrorAsync(string tenantId, string queueType, ProcessErrorEventArgs args)
    {
        logger.LogError(
            args.Exception,
            "ASB processor error for tenant {TenantId} queue '{QueueType}' — source: {ErrorSource}",
            tenantId,
            queueType,
            args.ErrorSource);

        return Task.CompletedTask;
    }
}
