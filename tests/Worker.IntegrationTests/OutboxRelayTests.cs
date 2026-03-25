namespace Worker.IntegrationTests;

/// <summary>
/// Integration tests for OutboxProcessorService using a real SQL Server (Testcontainers).
/// ASB is replaced by CaptureJobQueue / FailingJobQueue so no Azure connection is needed.
///
/// Each test truncates the OutboxMessages table in InitializeAsync to ensure full isolation
/// without the cost of spinning up a new container per test.
/// </summary>
public sealed class OutboxRelayTests(WorkerSqlServerFixture fixture)
    : IClassFixture<WorkerSqlServerFixture>, IAsyncLifetime
{
    // ── Setup / teardown ─────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        await using ApplicationDbContext context = fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync("DELETE FROM outbox.OutboxMessages");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private OutboxProcessorService CreateService(IJobQueue jobQueue)
    {
        ITenantRegistry registry = Substitute.For<ITenantRegistry>();
        registry.GetAllAsync().Returns([new TenantInfo("tenant-test", fixture.ConnectionString)]);

        return new OutboxProcessorService(
            registry,
            jobQueue,
            Options.Create(new WorkerOptions { MaxConcurrentTenants = 1 }),
            NullLogger<OutboxProcessorService>.Instance);
    }

    private TenantInfo TestTenant => new("tenant-test", fixture.ConnectionString);

    private static OutboxMessage PendingMessage() => new()
    {
        MessageType = OutboxMessageTypes.DomainEvent,
        QueueType = QueueTypes.Default,
        Type = "TestDomainEvent",
        Payload = "{}",
        OccurredOnUtc = DateTime.UtcNow
    };

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessTenant_ShouldRelayMessage_AndMarkAsRelayed()
    {
        // Arrange
        await using ApplicationDbContext context = fixture.CreateContext();
        OutboxMessage message = PendingMessage();
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        CaptureJobQueue queue = new();

        // Act
        await CreateService(queue).ProcessTenantOutboxAsync(TestTenant, runReaper: false, CancellationToken.None);

        // Assert — DB state reflects successful relay
        await context.Entry(message).ReloadAsync();
        message.IsRelayed.ShouldBeTrue();
        message.RelayedAt.ShouldNotBeNull();
        message.ProcessingStartedAt.ShouldBeNull();
        message.Error.ShouldBeNull();

        // Assert — message was forwarded with correct identity
        queue.Captured.ShouldHaveSingleItem();
        queue.Captured[0].TenantId.ShouldBe("tenant-test");
        queue.Captured[0].Message.IdempotencyKey.ShouldBe(message.Id.ToString());
        queue.Captured[0].Message.MessageType.ShouldBe(OutboxMessageTypes.DomainEvent);
    }

    [Fact]
    public async Task ProcessTenant_WhenNoMessages_ShouldNotEnqueueAnything()
    {
        // Arrange — outbox is empty
        CaptureJobQueue queue = new();

        // Act
        await CreateService(queue).ProcessTenantOutboxAsync(TestTenant, runReaper: false, CancellationToken.None);

        // Assert
        queue.Captured.ShouldBeEmpty();
    }

    [Fact]
    public async Task ProcessTenant_WhenEnqueueFails_ShouldResetClaim_AndPreserveError()
    {
        // Arrange
        await using ApplicationDbContext context = fixture.CreateContext();
        OutboxMessage message = PendingMessage();
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        // Act
        await CreateService(new FailingJobQueue()).ProcessTenantOutboxAsync(TestTenant, runReaper: false, CancellationToken.None);

        // Assert — message is left pending so the next poll retries it
        await context.Entry(message).ReloadAsync();
        message.IsRelayed.ShouldBeFalse();
        message.ProcessingStartedAt.ShouldBeNull(); // claim reset — not stuck
        message.Error.ShouldNotBeNull();            // failure reason preserved for ops
    }

    [Fact]
    public async Task ProcessTenant_WhenEnqueueFails_ShouldStillRelayOtherMessages()
    {
        // Arrange — two messages; the first succeeds, the second fails
        await using ApplicationDbContext context = fixture.CreateContext();

        // OccurredOnUtc ordering controls which message is picked first
        OutboxMessage first = PendingMessage() with { OccurredOnUtc = DateTime.UtcNow.AddSeconds(-10) };
        OutboxMessage second = PendingMessage() with { OccurredOnUtc = DateTime.UtcNow };
        context.OutboxMessages.AddRange(first, second);
        await context.SaveChangesAsync();

        // Queue fails only on the second call
        int callCount = 0;
        IJobQueue partialFailQueue = Substitute.For<IJobQueue>();
        partialFailQueue
            .EnqueueAsync(Arg.Any<string>(), Arg.Any<JobMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callCount++;
                if (callCount == 2)
                {
                    throw new InvalidOperationException("Second message fails");
                }
                return Task.CompletedTask;
            });

        // Act
        await CreateService(partialFailQueue).ProcessTenantOutboxAsync(TestTenant, runReaper: false, CancellationToken.None);

        // Assert — first message relayed, second left for retry
        await context.Entry(first).ReloadAsync();
        await context.Entry(second).ReloadAsync();

        first.IsRelayed.ShouldBeTrue();
        second.IsRelayed.ShouldBeFalse();
        second.ProcessingStartedAt.ShouldBeNull();
        second.Error.ShouldNotBeNull();
    }

    [Fact]
    public async Task ProcessTenant_Reaper_ShouldResetStaleClaimedMessages_ThenRelayThem()
    {
        // Arrange — simulate a message stuck in "Processing" from a previous worker crash.
        // ProcessingStartedAt is older than the 5-minute reaper threshold.
        await using ApplicationDbContext context = fixture.CreateContext();

        OutboxMessage stuckMessage = PendingMessage();
        context.OutboxMessages.Add(stuckMessage);
        await context.SaveChangesAsync();

        // Manually set ProcessingStartedAt to simulate a crash during a previous relay cycle
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE outbox.OutboxMessages SET ProcessingStartedAt = DATEADD(minute, -10, GETUTCDATE()) WHERE Id = {0}",
            stuckMessage.Id);

        CaptureJobQueue queue = new();

        // Act — run with reaper enabled; it resets the stale claim, then normal relay picks it up
        await CreateService(queue).ProcessTenantOutboxAsync(TestTenant, runReaper: true, CancellationToken.None);

        // Assert — message was recovered and relayed in the same cycle
        await context.Entry(stuckMessage).ReloadAsync();
        stuckMessage.IsRelayed.ShouldBeTrue();
        queue.Captured.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ProcessTenant_Reaper_ShouldNotResetRecentClaims()
    {
        // Arrange — message was claimed recently (within the 5-minute threshold);
        // the reaper must not reset it (another worker instance may be processing it).
        await using ApplicationDbContext context = fixture.CreateContext();

        OutboxMessage recentMessage = PendingMessage();
        context.OutboxMessages.Add(recentMessage);
        await context.SaveChangesAsync();

        await context.Database.ExecuteSqlRawAsync(
            "UPDATE outbox.OutboxMessages SET ProcessingStartedAt = DATEADD(minute, -1, GETUTCDATE()) WHERE Id = {0}",
            recentMessage.Id);

        CaptureJobQueue queue = new();

        // Act
        await CreateService(queue).ProcessTenantOutboxAsync(TestTenant, runReaper: true, CancellationToken.None);

        // Assert — reaper left the recent claim alone; READPAST skipped the locked row
        queue.Captured.ShouldBeEmpty();

        await context.Entry(recentMessage).ReloadAsync();
        recentMessage.IsRelayed.ShouldBeFalse();
        recentMessage.ProcessingStartedAt.ShouldNotBeNull();
    }
}
