using Application.Jobs.Schedules.BulkAdd;
using Application.Jobs.Schedules.BulkDelete;
using Application.Jobs.Schedules.BulkUpdate;
using AddScheduleResult = Application.Jobs.Schedules.BulkAdd.ScheduleResult;
using UpdateScheduleResult = Application.Jobs.Schedules.BulkUpdate.ScheduleResult;

namespace Application.IntegrationTests.Jobs.Schedules;

public sealed class BulkJobScheduleHandlerTests(ApplicationSqlServerFixture fixture)
    : IClassFixture<ApplicationSqlServerFixture>
{
    // ── BulkAdd ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task BulkAddSchedules_ShouldPersistAllValidSchedules()
    {
        Job job = await SeedJobAsync();
        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new BulkAddJobSchedulesCommandHandler(context, AlwaysValidCronValidator.Instance);

        var command = new BulkAddJobSchedulesCommand
        {
            JobId = job.Id,
            Schedules =
            [
                new BulkAddScheduleItem
                {
                    Name = "Morning",
                    CronExpression = "0 0 8 * * ?",
                    TimeZoneId = "UTC"
                },
                new BulkAddScheduleItem
                {
                    Name = "Evening",
                    CronExpression = "0 0 18 * * ?",
                    TimeZoneId = "UTC"
                }
            ]
        };

        Result<BulkOperationResponse<AddScheduleResult>> result =
            await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Succeeded.Count.ShouldBe(2);
        result.Value.Failed.ShouldBeEmpty();

        await using ApplicationDbContext verify = fixture.CreateContext();
        Job? loaded = await verify.Jobs
            .Include(j => j.Schedules)
            .FirstOrDefaultAsync(j => j.Id == job.Id);

        loaded!.Schedules.Count.ShouldBe(3); // 1 from seed + 2 added
    }

    [Fact]
    public async Task BulkAddSchedules_WithMixOfValidAndInvalid_ShouldPartiallySucceed()
    {
        Job job = await SeedJobAsync();
        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new BulkAddJobSchedulesCommandHandler(context, AlwaysValidCronValidator.Instance);

        var command = new BulkAddJobSchedulesCommand
        {
            JobId = job.Id,
            Schedules =
            [
                new BulkAddScheduleItem
                {
                    Name = "Valid",
                    CronExpression = "0 0 10 * * ?",
                    TimeZoneId = "UTC"
                },
                new BulkAddScheduleItem
                {
                    Name = "", // invalid — empty name
                    CronExpression = "0 0 10 * * ?",
                    TimeZoneId = "UTC"
                },
                new BulkAddScheduleItem
                {
                    Name = "Bad TZ",
                    CronExpression = "0 0 10 * * ?",
                    TimeZoneId = "Fake/Zone" // domain rejects
                }
            ]
        };

        Result<BulkOperationResponse<AddScheduleResult>> result =
            await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Succeeded.Count.ShouldBe(1);
        result.Value.Succeeded[0].Name.ShouldBe("Valid");

        result.Value.Failed.Count.ShouldBe(2);
        result.Value.Failed[0].Index.ShouldBe(1); // empty name
        result.Value.Failed[1].Index.ShouldBe(2); // bad timezone
    }

    [Fact]
    public async Task BulkAddSchedules_WhenJobNotFound_ShouldReturnFailure()
    {
        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new BulkAddJobSchedulesCommandHandler(context, AlwaysValidCronValidator.Instance);

        var command = new BulkAddJobSchedulesCommand
        {
            JobId = Guid.NewGuid(),
            Schedules =
            [
                new BulkAddScheduleItem
                {
                    Name = "Daily",
                    CronExpression = "0 0 10 * * ?",
                    TimeZoneId = "UTC"
                }
            ]
        };

        Result<BulkOperationResponse<AddScheduleResult>> result =
            await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Job.NotFound");
    }

    // ── BulkUpdate ──────────────────────────────────────────────────────────

    [Fact]
    public async Task BulkUpdateSchedules_ShouldPersistChanges()
    {
        Job job = await SeedJobAsync();
        (Guid scheduleId, byte[] rowVersion) = await SeedScheduleAsync(job.Id, "Original", "0 0 10 * * ?");

        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new BulkUpdateJobSchedulesCommandHandler(context, AlwaysValidCronValidator.Instance);

        var command = new BulkUpdateJobSchedulesCommand
        {
            JobId = job.Id,
            Schedules =
            [
                new BulkUpdateScheduleItem
                {
                    ScheduleId = scheduleId,
                    Name = "Updated",
                    CronExpression = "0 0 12 * * ?",
                    TimeZoneId = "UTC",
                    IsEnabled = false,
                    RowVersion = rowVersion
                }
            ]
        };

        Result<BulkOperationResponse<UpdateScheduleResult>> result =
            await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Succeeded.Count.ShouldBe(1);

        await using ApplicationDbContext verify = fixture.CreateContext();
        Job? loaded = await verify.Jobs
            .Include(j => j.Schedules)
            .FirstOrDefaultAsync(j => j.Id == job.Id);

        JobSchedule schedule = loaded!.Schedules.Single(s => s.Id == scheduleId);
        schedule.Name.ShouldBe("Updated");
        schedule.CronExpression.ShouldBe("0 0 12 * * ?");
        schedule.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task BulkUpdateSchedules_WithMixOfValidAndNotFound_ShouldPartiallySucceed()
    {
        Job job = await SeedJobAsync();
        (Guid existingId, byte[] rowVersion) = await SeedScheduleAsync(job.Id, "Existing", "0 0 10 * * ?");

        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new BulkUpdateJobSchedulesCommandHandler(context, AlwaysValidCronValidator.Instance);

        var command = new BulkUpdateJobSchedulesCommand
        {
            JobId = job.Id,
            Schedules =
            [
                new BulkUpdateScheduleItem
                {
                    ScheduleId = existingId,
                    Name = "Updated",
                    CronExpression = "0 0 12 * * ?",
                    TimeZoneId = "UTC",
                    IsEnabled = true,
                    RowVersion = rowVersion
                },
                new BulkUpdateScheduleItem
                {
                    ScheduleId = Guid.NewGuid(), // does not exist
                    Name = "Ghost",
                    CronExpression = "0 0 12 * * ?",
                    TimeZoneId = "UTC",
                    IsEnabled = true,
                    RowVersion = [0, 0, 0, 0, 0, 0, 0, 1]
                }
            ]
        };

        Result<BulkOperationResponse<UpdateScheduleResult>> result =
            await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Succeeded.Count.ShouldBe(1);
        result.Value.Failed.Count.ShouldBe(1);
        result.Value.Failed[0].Index.ShouldBe(1);
    }

    // ── BulkDelete ──────────────────────────────────────────────────────────

    [Fact]
    public async Task BulkDeleteSchedules_ShouldRemoveSchedules()
    {
        Job job = await SeedJobAsync();
        (Guid id1, byte[] rv1) = await SeedScheduleAsync(job.Id, "S1", "0 0 10 * * ?");
        (Guid id2, byte[] rv2) = await SeedScheduleAsync(job.Id, "S2", "0 0 12 * * ?");

        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new BulkDeleteJobSchedulesCommandHandler(context);

        var command = new BulkDeleteJobSchedulesCommand
        {
            JobId = job.Id,
            Schedules =
            [
                new BulkDeleteScheduleItem { ScheduleId = id1, RowVersion = rv1 },
                new BulkDeleteScheduleItem { ScheduleId = id2, RowVersion = rv2 }
            ]
        };

        Result<BulkOperationResponse<ScheduleDeleteResult>> result =
            await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Succeeded.Count.ShouldBe(2);
        result.Value.Failed.ShouldBeEmpty();

        await using ApplicationDbContext verify = fixture.CreateContext();
        Job? loaded = await verify.Jobs
            .Include(j => j.Schedules)
            .FirstOrDefaultAsync(j => j.Id == job.Id);

        loaded!.Schedules.ShouldNotContain(s => s.Id == id1 || s.Id == id2);
    }

    [Fact]
    public async Task BulkDeleteSchedules_WithMixOfExistingAndMissing_ShouldPartiallySucceed()
    {
        Job job = await SeedJobAsync();
        (Guid existingId, byte[] rv) = await SeedScheduleAsync(job.Id, "Existing", "0 0 10 * * ?");

        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new BulkDeleteJobSchedulesCommandHandler(context);

        var command = new BulkDeleteJobSchedulesCommand
        {
            JobId = job.Id,
            Schedules =
            [
                new BulkDeleteScheduleItem { ScheduleId = existingId, RowVersion = rv },
                new BulkDeleteScheduleItem { ScheduleId = Guid.NewGuid(), RowVersion = [0, 0, 0, 0, 0, 0, 0, 1] }
            ]
        };

        Result<BulkOperationResponse<ScheduleDeleteResult>> result =
            await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Succeeded.Count.ShouldBe(1);
        result.Value.Failed.Count.ShouldBe(1);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

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

    private async Task<(Guid Id, byte[] RowVersion)> SeedScheduleAsync(Guid jobId, string name, string cron)
    {
        await using ApplicationDbContext context = fixture.CreateContext();

        Job job = await context.Jobs
            .Include(j => j.Schedules)
            .SingleAsync(j => j.Id == jobId);

        Result<Guid> result = job.AddSchedule(name, cron, "UTC");
        await context.SaveChangesAsync();

        JobSchedule schedule = job.Schedules.First(s => s.Id == result.Value);
        return (result.Value, schedule.RowVersion);
    }
}
