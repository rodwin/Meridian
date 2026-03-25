using Application.Jobs.Schedules.BulkUpdate;
using Application.Jobs.Schedules.Delete;
using Application.Jobs.Schedules.Update;
using UpdateScheduleResult = Application.Jobs.Schedules.BulkUpdate.ScheduleResult;

namespace Application.IntegrationTests.Jobs.Schedules;

/// <summary>
/// Verifies that stale RowVersion values cause DbUpdateConcurrencyException
/// for schedule update and delete operations.
/// </summary>
public sealed class ConcurrencyTests(ApplicationSqlServerFixture fixture)
    : IClassFixture<ApplicationSqlServerFixture>
{
    [Fact]
    public async Task UpdateSchedule_WithStaleRowVersion_ShouldThrowConcurrencyException()
    {
        // Arrange — seed a schedule and capture its RowVersion
        Job job = await SeedJobAsync();
        (Guid scheduleId, byte[] originalRowVersion) = await SeedScheduleAsync(job.Id);

        // Simulate another request modifying the schedule first
        await using (ApplicationDbContext context1 = fixture.CreateContext())
        {
            var firstUpdate = new UpdateJobScheduleCommandHandler(context1);
            Result result = await firstUpdate.Handle(new UpdateJobScheduleCommand
            {
                JobId = job.Id,
                ScheduleId = scheduleId,
                Name = "Updated by first request",
                CronExpression = "0 0 12 * * ?",
                TimeZoneId = "UTC",
                IsEnabled = true,
                RowVersion = originalRowVersion
            }, CancellationToken.None);
            result.IsSuccess.ShouldBeTrue();
        }

        // Act — attempt a second update with the now-stale RowVersion
        await using ApplicationDbContext context2 = fixture.CreateContext();
        var secondUpdate = new UpdateJobScheduleCommandHandler(context2);

        DbUpdateConcurrencyException exception = await Should.ThrowAsync<DbUpdateConcurrencyException>(
            () => secondUpdate.Handle(new UpdateJobScheduleCommand
            {
                JobId = job.Id,
                ScheduleId = scheduleId,
                Name = "Should conflict",
                CronExpression = "0 0 14 * * ?",
                TimeZoneId = "UTC",
                IsEnabled = true,
                RowVersion = originalRowVersion // stale
            }, CancellationToken.None));

        exception.ShouldNotBeNull();
    }

    [Fact]
    public async Task DeleteSchedule_WithStaleRowVersion_ShouldThrowConcurrencyException()
    {
        // Arrange
        Job job = await SeedJobAsync();
        (Guid scheduleId, byte[] originalRowVersion) = await SeedScheduleAsync(job.Id);

        // Simulate another request modifying the schedule first
        await using (ApplicationDbContext context1 = fixture.CreateContext())
        {
            var update = new UpdateJobScheduleCommandHandler(context1);
            Result result = await update.Handle(new UpdateJobScheduleCommand
            {
                JobId = job.Id,
                ScheduleId = scheduleId,
                Name = "Modified",
                CronExpression = "0 0 12 * * ?",
                TimeZoneId = "UTC",
                IsEnabled = true,
                RowVersion = originalRowVersion
            }, CancellationToken.None);
            result.IsSuccess.ShouldBeTrue();
        }

        // Act — attempt delete with stale RowVersion
        await using ApplicationDbContext context2 = fixture.CreateContext();
        var handler = new DeleteJobScheduleCommandHandler(context2);

        DbUpdateConcurrencyException exception = await Should.ThrowAsync<DbUpdateConcurrencyException>(
            () => handler.Handle(new DeleteJobScheduleCommand
            {
                JobId = job.Id,
                ScheduleId = scheduleId,
                RowVersion = originalRowVersion // stale
            }, CancellationToken.None));

        exception.ShouldNotBeNull();
    }

    [Fact]
    public async Task BulkUpdateSchedules_WithStaleRowVersion_ShouldThrowConcurrencyException()
    {
        // Arrange
        Job job = await SeedJobAsync();
        (Guid scheduleId, byte[] originalRowVersion) = await SeedScheduleAsync(job.Id);

        // Simulate another request modifying the schedule first
        await using (ApplicationDbContext context1 = fixture.CreateContext())
        {
            var firstUpdate = new UpdateJobScheduleCommandHandler(context1);
            Result result = await firstUpdate.Handle(new UpdateJobScheduleCommand
            {
                JobId = job.Id,
                ScheduleId = scheduleId,
                Name = "Updated by first request",
                CronExpression = "0 0 12 * * ?",
                TimeZoneId = "UTC",
                IsEnabled = true,
                RowVersion = originalRowVersion
            }, CancellationToken.None);
            result.IsSuccess.ShouldBeTrue();
        }

        // Act — bulk update with stale RowVersion
        await using ApplicationDbContext context2 = fixture.CreateContext();
        var handler = new BulkUpdateJobSchedulesCommandHandler(context2, AlwaysValidCronValidator.Instance);

        DbUpdateConcurrencyException exception = await Should.ThrowAsync<DbUpdateConcurrencyException>(
            () => handler.Handle(new BulkUpdateJobSchedulesCommand
            {
                JobId = job.Id,
                Schedules =
                [
                    new BulkUpdateScheduleItem
                    {
                        ScheduleId = scheduleId,
                        Name = "Should conflict",
                        CronExpression = "0 0 14 * * ?",
                        TimeZoneId = "UTC",
                        IsEnabled = true,
                        RowVersion = originalRowVersion // stale
                    }
                ]
            }, CancellationToken.None));

        exception.ShouldNotBeNull();
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
