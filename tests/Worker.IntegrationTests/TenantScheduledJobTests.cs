namespace Worker.IntegrationTests;

/// <summary>
/// Integration tests for TenantScheduledJob — the Quartz IJob that fires when a
/// schedule trigger is due and writes an OutboxMessage to the tenant's database.
///
/// Uses a real SQL Server container (via WorkerSqlServerFixture) so the outbox
/// write is verified against the actual schema, not mocks.
/// </summary>
public sealed class TenantScheduledJobTests(WorkerSqlServerFixture fixture)
    : IClassFixture<WorkerSqlServerFixture>
{
    private const string TenantId = "tenant-x";

    // ── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Execute_ShouldWriteScheduledTriggerOutboxMessage()
    {
        // Arrange
        string jobId = Guid.NewGuid().ToString();
        string scheduleId = Guid.NewGuid().ToString();

        IJobExecutionContext jobContext = BuildJobContext(jobId, scheduleId);
        TenantScheduledJob job = new(NullLogger<TenantScheduledJob>.Instance);

        // Act
        await job.Execute(jobContext);

        // Assert — one outbox message with the correct type and payload
        await using ApplicationDbContext context = fixture.CreateContext();
        OutboxMessage? message = await context.OutboxMessages
            .FirstOrDefaultAsync(m => m.Payload.Contains(jobId));

        message.ShouldNotBeNull();
        message.MessageType.ShouldBe(OutboxMessageTypes.ScheduledTrigger);
        message.Payload.ShouldContain(jobId);
        message.Payload.ShouldContain(scheduleId);
        message.IsRelayed.ShouldBeFalse();
        message.OccurredOnUtc.ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Execute_ShouldWriteSeparateMessagePerFiring()
    {
        // Arrange — fire the same schedule twice (simulating two cron ticks)
        string jobId = Guid.NewGuid().ToString();
        string scheduleId = Guid.NewGuid().ToString();

        TenantScheduledJob job = new(NullLogger<TenantScheduledJob>.Instance);

        // Act
        await job.Execute(BuildJobContext(jobId, scheduleId));
        await job.Execute(BuildJobContext(jobId, scheduleId));

        // Assert — two independent outbox messages for this specific job
        await using ApplicationDbContext context = fixture.CreateContext();
        int count = await context.OutboxMessages
            .Where(m => m.MessageType == OutboxMessageTypes.ScheduledTrigger
                && m.Payload.Contains(jobId))
            .CountAsync();

        count.ShouldBe(2);
    }

    [Fact]
    public async Task Execute_ShouldUseLongRunningQueueType()
    {
        string jobId = Guid.NewGuid().ToString();
        TenantScheduledJob job = new(NullLogger<TenantScheduledJob>.Instance);
        await job.Execute(BuildJobContext(jobId, Guid.NewGuid().ToString()));

        await using ApplicationDbContext context = fixture.CreateContext();
        OutboxMessage? message = await context.OutboxMessages
            .FirstOrDefaultAsync(m => m.Payload.Contains(jobId));

        message.ShouldNotBeNull();
        message.QueueType.ShouldBe(QueueTypes.LongRunning);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private IJobExecutionContext BuildJobContext(string jobId, string scheduleId)
    {
        var dataMap = new JobDataMap
        {
            { "TenantId", TenantId },
            { "JobId", jobId },
            { "ScheduleId", scheduleId },
            { "ConnectionString", fixture.ConnectionString }
        };

        IJobExecutionContext context = Substitute.For<IJobExecutionContext>();
        context.MergedJobDataMap.Returns(dataMap);
        context.CancellationToken.Returns(CancellationToken.None);

        return context;
    }
}
