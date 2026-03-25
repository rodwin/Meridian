using Application.Jobs.Schedules.Add;
using Application.Jobs.Schedules.Delete;
using Application.Jobs.Schedules.Update;

namespace Application.IntegrationTests.Jobs.Schedules;

/// <summary>
/// Integration tests for AddJobScheduleCommandHandler, UpdateJobScheduleCommandHandler,
/// and DeleteJobScheduleCommandHandler using a real SQL Server (Testcontainers).
///
/// Each test seeds its own Job and cleans up via a unique JobId so tests are fully isolated
/// without needing a fresh container per test.
/// </summary>
public sealed class JobScheduleHandlerTests(ApplicationSqlServerFixture fixture)
    : IClassFixture<ApplicationSqlServerFixture>
{
    // ── AddJobScheduleCommandHandler ─────────────────────────────────────────

    [Fact]
    public async Task AddSchedule_ShouldPersistScheduleToDatabase()
    {
        // Arrange
        Job job = await SeedJobAsync();
        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new AddJobScheduleCommandHandler(context);

        var command = new AddJobScheduleCommand
        {
            JobId = job.Id,
            Name = "Daily 10am",
            CronExpression = "0 0 10 * * ?",
            TimeZoneId = "UTC"
        };

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert — handler returns success with the new schedule's Id
        result.IsSuccess.ShouldBeTrue();

        // Assert — schedule is persisted in the database
        await using ApplicationDbContext verify = fixture.CreateContext();
        Job? loaded = await verify.Jobs
            .Include(j => j.Schedules)
            .FirstOrDefaultAsync(j => j.Id == job.Id);

        loaded.ShouldNotBeNull();
        JobSchedule added = loaded.Schedules.Single(s => s.Id == result.Value);
        added.Name.ShouldBe("Daily 10am");
        added.CronExpression.ShouldBe("0 0 10 * * ?");
        added.TimeZoneId.ShouldBe("UTC");
        added.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task AddSchedule_WhenJobNotFound_ShouldReturnFailure()
    {
        // Arrange
        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new AddJobScheduleCommandHandler(context);

        var command = new AddJobScheduleCommand
        {
            JobId = Guid.NewGuid(),
            Name = "Daily",
            CronExpression = "0 0 10 * * ?",
            TimeZoneId = "UTC"
        };

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Job.NotFound");
    }

    [Fact]
    public async Task AddSchedule_WhenInvalidTimeZone_ShouldReturnFailure_AndNotPersist()
    {
        // Arrange
        Job job = await SeedJobAsync();
        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new AddJobScheduleCommandHandler(context);

        var command = new AddJobScheduleCommand
        {
            JobId = job.Id,
            Name = "Daily",
            CronExpression = "0 0 10 * * ?",
            TimeZoneId = "Bad/Zone"
        };

        // Act
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Job.InvalidTimeZone");

        await using ApplicationDbContext verify = fixture.CreateContext();
        Job? loaded = await verify.Jobs
            .Include(j => j.Schedules)
            .FirstOrDefaultAsync(j => j.Id == job.Id);

        loaded!.Schedules.ShouldNotContain(s => s.TimeZoneId == "Bad/Zone");
    }

    // ── UpdateJobScheduleCommandHandler ──────────────────────────────────────

    [Fact]
    public async Task UpdateSchedule_ShouldPersistChangesToDatabase()
    {
        // Arrange
        Job job = await SeedJobAsync();
        (Guid scheduleId, byte[] rowVersion) = await SeedScheduleAsync(job.Id);

        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new UpdateJobScheduleCommandHandler(context);

        var command = new UpdateJobScheduleCommand
        {
            JobId = job.Id,
            ScheduleId = scheduleId,
            Name = "Weekly Monday",
            CronExpression = "0 0 10 ? * MON",
            TimeZoneId = "UTC",
            IsEnabled = false,
            RowVersion = rowVersion
        };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        await using ApplicationDbContext verify = fixture.CreateContext();
        Job? loaded = await verify.Jobs
            .Include(j => j.Schedules)
            .FirstOrDefaultAsync(j => j.Id == job.Id);

        JobSchedule schedule = loaded!.Schedules.Single(s => s.Id == scheduleId);
        schedule.Name.ShouldBe("Weekly Monday");
        schedule.CronExpression.ShouldBe("0 0 10 ? * MON");
        schedule.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateSchedule_WhenJobNotFound_ShouldReturnFailure()
    {
        // Arrange
        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new UpdateJobScheduleCommandHandler(context);

        var command = new UpdateJobScheduleCommand
        {
            JobId = Guid.NewGuid(),
            ScheduleId = Guid.NewGuid(),
            Name = "Daily",
            CronExpression = "0 0 10 * * ?",
            TimeZoneId = "UTC",
            IsEnabled = true
        };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Job.NotFound");
    }

    [Fact]
    public async Task UpdateSchedule_WhenScheduleNotFound_ShouldReturnFailure()
    {
        // Arrange
        Job job = await SeedJobAsync();
        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new UpdateJobScheduleCommandHandler(context);

        var command = new UpdateJobScheduleCommand
        {
            JobId = job.Id,
            ScheduleId = Guid.NewGuid(),
            Name = "Daily",
            CronExpression = "0 0 10 * * ?",
            TimeZoneId = "UTC",
            IsEnabled = true
        };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Job.ScheduleNotFound");
    }

    // ── DeleteJobScheduleCommandHandler ──────────────────────────────────────

    [Fact]
    public async Task DeleteSchedule_ShouldRemoveScheduleFromDatabase()
    {
        // Arrange
        Job job = await SeedJobAsync();
        (Guid scheduleId, byte[] rowVersion) = await SeedScheduleAsync(job.Id);

        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new DeleteJobScheduleCommandHandler(context);

        var command = new DeleteJobScheduleCommand
        {
            JobId = job.Id,
            ScheduleId = scheduleId,
            RowVersion = rowVersion
        };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        await using ApplicationDbContext verify = fixture.CreateContext();
        Job? loaded = await verify.Jobs
            .Include(j => j.Schedules)
            .FirstOrDefaultAsync(j => j.Id == job.Id);

        loaded!.Schedules.ShouldNotContain(s => s.Id == scheduleId);
    }

    [Fact]
    public async Task DeleteSchedule_WhenJobNotFound_ShouldReturnFailure()
    {
        // Arrange
        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new DeleteJobScheduleCommandHandler(context);

        var command = new DeleteJobScheduleCommand
        {
            JobId = Guid.NewGuid(),
            ScheduleId = Guid.NewGuid()
        };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Job.NotFound");
    }

    [Fact]
    public async Task DeleteSchedule_WhenScheduleNotFound_ShouldReturnFailure()
    {
        // Arrange
        Job job = await SeedJobAsync();
        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new DeleteJobScheduleCommandHandler(context);

        var command = new DeleteJobScheduleCommand
        {
            JobId = job.Id,
            ScheduleId = Guid.NewGuid()
        };

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Job.ScheduleNotFound");
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

    private async Task<(Guid Id, byte[] RowVersion)> SeedScheduleAsync(Guid jobId)
    {
        await using ApplicationDbContext context = fixture.CreateContext();

        Job job = await context.Jobs
            .Include(j => j.Schedules)
            .SingleAsync(j => j.Id == jobId);

        Result<Guid> result = job.AddSchedule("Daily", "0 0 10 * * ?", "UTC");
        await context.SaveChangesAsync();

        JobSchedule schedule = job.Schedules.First(s => s.Id == result.Value);
        return (result.Value, schedule.RowVersion);
    }
}
