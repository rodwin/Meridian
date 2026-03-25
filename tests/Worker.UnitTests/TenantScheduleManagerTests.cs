namespace Worker.UnitTests;

/// <summary>
/// Unit tests for TenantScheduleManager — the component that bridges the tenant
/// registry with Quartz's in-memory scheduler.
///
/// Each enabled JobSchedule in the DB becomes one independent Quartz trigger.
/// Tests verify that triggers are registered correctly, stale triggers are cleaned
/// up before re-registration, and repository errors are swallowed so one bad tenant
/// never blocks others.
/// </summary>
public sealed class TenantScheduleManagerTests
{
    private readonly ITenantRegistry _registry = Substitute.For<ITenantRegistry>();
    private readonly ISchedulerFactory _schedulerFactory = Substitute.For<ISchedulerFactory>();
    private readonly IScheduler _scheduler = Substitute.For<IScheduler>();
    private readonly IServiceScopeFactory _scopeFactory = Substitute.For<IServiceScopeFactory>();

    public TenantScheduleManagerTests()
    {
        _schedulerFactory
            .GetScheduler(Arg.Any<CancellationToken>())
            .Returns(_scheduler);

        // Default: no existing triggers for any tenant group
        _scheduler
            .GetJobKeys(Arg.Any<GroupMatcher<JobKey>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<JobKey>());
    }

    // ── LoadAllAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAllAsync_WhenNoTenants_ShouldNotScheduleAnyTriggers()
    {
        _registry.GetAllAsync().Returns([]);

        await CreateManager().LoadAllAsync(CancellationToken.None);

        await _scheduler
            .DidNotReceive()
            .ScheduleJob(Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadAllAsync_ShouldRegisterOneQuartzTriggerPerEnabledSchedule()
    {
        // A single job with two schedules → two independent Quartz triggers
        TenantInfo tenant = new("tenant-a", "Server=.;Database=test;TrustServerCertificate=True");
        _registry.GetAllAsync().Returns([tenant]);

        Guid jobId = Guid.NewGuid();
        ScheduledJobDto schedule1 = new(jobId, Guid.NewGuid(), "0 0 10 * * ?", "UTC");
        ScheduledJobDto schedule2 = new(jobId, Guid.NewGuid(), "0 0 18 * * ?", "UTC");
        SetupScopeReturning([schedule1, schedule2]);

        await CreateManager().LoadAllAsync(CancellationToken.None);

        await _scheduler
            .Received(2)
            .ScheduleJob(Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadAllAsync_ShouldSetScheduleIdAsQuartzJobName()
    {
        TenantInfo tenant = new("tenant-a", "Server=.;Database=test;TrustServerCertificate=True");
        _registry.GetAllAsync().Returns([tenant]);

        Guid jobId = Guid.NewGuid();
        Guid scheduleId = Guid.NewGuid();
        ScheduledJobDto schedule = new(jobId, scheduleId, "0 0 * * * ?", "UTC");
        SetupScopeReturning([schedule]);

        await CreateManager().LoadAllAsync(CancellationToken.None);

        await _scheduler
            .Received(1)
            .ScheduleJob(
                Arg.Is<IJobDetail>(d =>
                    d.Key.Group == tenant.TenantId &&
                    d.Key.Name == scheduleId.ToString() &&
                    d.JobDataMap.GetString("JobId") == jobId.ToString() &&
                    d.JobDataMap.GetString("ScheduleId") == scheduleId.ToString()),
                Arg.Any<ITrigger>(),
                Arg.Any<CancellationToken>());
    }

    // ── SyncTenantAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task SyncTenantAsync_WhenTriggerAlreadyRegistered_ShouldDeleteBeforeRescheduling()
    {
        TenantInfo tenant = new("tenant-b", "Server=.;Database=test;TrustServerCertificate=True");
        Guid scheduleId = Guid.NewGuid();
        ScheduledJobDto schedule = new(Guid.NewGuid(), scheduleId, "0 0 10 * * ?", "UTC");
        SetupScopeReturning([schedule]);

        JobKey existingKey = new(scheduleId.ToString(), tenant.TenantId);
        _scheduler
            .GetJobKeys(Arg.Any<GroupMatcher<JobKey>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<JobKey> { existingKey });

        await CreateManager().SyncTenantAsync(tenant, CancellationToken.None);

        await _scheduler.Received(1).DeleteJob(existingKey, Arg.Any<CancellationToken>());
        await _scheduler.Received(1).ScheduleJob(
            Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncTenantAsync_WhenAllSchedulesRemoved_ShouldDeleteStaleTriggersAndRegisterNone()
    {
        TenantInfo tenant = new("tenant-c", "Server=.;Database=test;TrustServerCertificate=True");
        SetupScopeReturning([]);

        JobKey staleKey = new(Guid.NewGuid().ToString(), tenant.TenantId);
        _scheduler
            .GetJobKeys(Arg.Any<GroupMatcher<JobKey>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<JobKey> { staleKey });

        await CreateManager().SyncTenantAsync(tenant, CancellationToken.None);

        await _scheduler.Received(1).DeleteJob(staleKey, Arg.Any<CancellationToken>());
        await _scheduler
            .DidNotReceive()
            .ScheduleJob(Arg.Any<IJobDetail>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncTenantAsync_WhenRepositoryThrows_ShouldNotPropagateException()
    {
        var tenant = new TenantInfo("tenant-d", "Server=.;Database=test;TrustServerCertificate=True");
        SetupScopeThrowingOn(new InvalidOperationException("DB unavailable"));

        // Should swallow the exception — errors are logged, never propagated,
        // so one broken tenant doesn't prevent others from syncing.
        Exception? thrown = await Record.ExceptionAsync(
            () => CreateManager().SyncTenantAsync(tenant, CancellationToken.None));

        thrown.ShouldBeNull();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private TenantScheduleManager CreateManager() =>
        new(_schedulerFactory, _registry, _scopeFactory, NullLogger<TenantScheduleManager>.Instance);

    private void SetupScopeReturning(IReadOnlyList<ScheduledJobDto> schedules)
    {
        IServiceScope scope = Substitute.For<IServiceScope>();
        IServiceProvider provider = Substitute.For<IServiceProvider>();
        TenantContext ctx = new();

        IScheduledJobRepository repo = Substitute.For<IScheduledJobRepository>();
        repo.GetEnabledSchedulesAsync(Arg.Any<CancellationToken>()).Returns(schedules);

        provider.GetService(typeof(TenantContext)).Returns(ctx);
        provider.GetService(typeof(IScheduledJobRepository)).Returns(repo);
        scope.ServiceProvider.Returns(provider);
        _scopeFactory.CreateScope().Returns(scope);
    }

    private void SetupScopeThrowingOn(Exception exception)
    {
        IServiceScope scope = Substitute.For<IServiceScope>();
        IServiceProvider provider = Substitute.For<IServiceProvider>();
        TenantContext ctx = new();

        IScheduledJobRepository repo = Substitute.For<IScheduledJobRepository>();
        repo.GetEnabledSchedulesAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<ScheduledJobDto>>(_ => throw exception);

        provider.GetService(typeof(TenantContext)).Returns(ctx);
        provider.GetService(typeof(IScheduledJobRepository)).Returns(repo);
        scope.ServiceProvider.Returns(provider);
        _scopeFactory.CreateScope().Returns(scope);
    }
}
