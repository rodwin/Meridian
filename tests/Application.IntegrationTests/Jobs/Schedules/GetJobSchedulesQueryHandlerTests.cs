using Application.Jobs.Get;
using Application.Jobs.Schedules.Get;

namespace Application.IntegrationTests.Jobs.Schedules;

public sealed class GetJobSchedulesQueryHandlerTests(ApplicationSqlServerFixture fixture)
    : IClassFixture<ApplicationSqlServerFixture>
{
    [Fact]
    public async Task GetSchedules_WhenJobHasSchedules_ShouldReturnAllSchedules()
    {
        // Arrange
        Job job = await SeedJobAsync();
        await AddScheduleAsync(job.Id, "Daily 10am", "0 0 10 * * ?", "UTC");
        await AddScheduleAsync(job.Id, "Weekly Monday", "0 0 8 ? * MON", "UTC");

        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new GetJobSchedulesQueryHandler(context);

        // Act
        Result<List<JobScheduleResponse>> result = await handler.Handle(
            new GetJobSchedulesQuery(job.Id),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(3); // 1 seeded by SeedJobAsync + 2 added above
    }

    [Fact]
    public async Task GetSchedules_WhenJobHasNoSchedules_ShouldReturnEmptyList()
    {
        // Arrange
        Job job = await SeedJobWithNoSchedulesAsync();
        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new GetJobSchedulesQueryHandler(context);

        // Act
        Result<List<JobScheduleResponse>> result = await handler.Handle(
            new GetJobSchedulesQuery(job.Id),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetSchedules_WhenJobNotFound_ShouldReturnFailure()
    {
        // Arrange
        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new GetJobSchedulesQueryHandler(context);

        // Act
        Result<List<JobScheduleResponse>> result = await handler.Handle(
            new GetJobSchedulesQuery(Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Job.NotFound");
    }

    [Fact]
    public async Task GetSchedules_ShouldMapScheduleFieldsCorrectly()
    {
        // Arrange
        Job job = await SeedJobAsync();
        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new GetJobSchedulesQueryHandler(context);

        // Act
        Result<List<JobScheduleResponse>> result = await handler.Handle(
            new GetJobSchedulesQuery(job.Id),
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        JobScheduleResponse schedule = result.Value[0];
        schedule.Id.ShouldNotBe(Guid.Empty);
        schedule.Name.ShouldBe("Daily");
        schedule.CronExpression.ShouldBe("0 0 10 * * ?");
        schedule.TimeZoneId.ShouldBe("UTC");
        schedule.IsEnabled.ShouldBeTrue();
        schedule.RowVersion.ShouldNotBeEmpty();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Job> SeedJobAsync()
    {
        await using ApplicationDbContext context = fixture.CreateContext();

        Job job = Job.Create(
            $"Test Job {Guid.NewGuid():N}",
            null,
            [("Step 1", "RunDatabaseLoad", null, OnFailureAction.Stop)],
            [("Daily", "0 0 10 * * ?", "UTC")]).Value;

        context.Jobs.Add(job);
        await context.SaveChangesAsync();
        return job;
    }

    private async Task<Job> SeedJobWithNoSchedulesAsync()
    {
        await using ApplicationDbContext context = fixture.CreateContext();

        Job job = Job.Create(
            $"Test Job {Guid.NewGuid():N}",
            null,
            [("Step 1", "RunDatabaseLoad", null, OnFailureAction.Stop)],
            []).Value;

        context.Jobs.Add(job);
        await context.SaveChangesAsync();
        return job;
    }

    private async Task AddScheduleAsync(Guid jobId, string name, string cron, string timeZone)
    {
        await using ApplicationDbContext context = fixture.CreateContext();

        Job job = await context.Jobs
            .Include(j => j.Schedules)
            .SingleAsync(j => j.Id == jobId);

        job.AddSchedule(name, cron, timeZone);
        await context.SaveChangesAsync();
    }
}
