namespace Application.UnitTests.Jobs;

/// <summary>
/// Unit tests for the Job aggregate's schedule management behaviour.
/// These are pure domain tests — no database, no infrastructure.
/// </summary>
public sealed class JobScheduleTests
{
    // ── AddSchedule ───────────────────────────────────────────────────────────

    [Fact]
    public void AddSchedule_ShouldAddScheduleToCollection()
    {
        Job job = CreateJob();

        job.AddSchedule("Weekly", "0 0 10 ? * MON", "UTC");

        job.Schedules.Count.ShouldBe(2);
        JobSchedule added = job.Schedules[job.Schedules.Count - 1];
        added.Name.ShouldBe("Weekly");
        added.CronExpression.ShouldBe("0 0 10 ? * MON");
        added.TimeZoneId.ShouldBe("UTC");
        added.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void AddSchedule_ShouldReturnNewScheduleId()
    {
        Job job = CreateJob();

        Result<Guid> result = job.AddSchedule("Weekly", "0 0 10 ? * MON", "UTC");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(job.Schedules[job.Schedules.Count - 1].Id);
    }

    [Fact]
    public void AddSchedule_ShouldRaiseJobScheduleAddedDomainEvent()
    {
        Job job = CreateJob();

        Result<Guid> result = job.AddSchedule("Weekly", "0 0 10 ? * MON", "UTC");

        job.DomainEvents.ShouldHaveSingleItem();
        var @event = job.DomainEvents[0].ShouldBeOfType<JobScheduleAddedDomainEvent>();
        @event.JobId.ShouldBe(job.Id);
        @event.ScheduleId.ShouldBe(result.Value);
    }

    [Fact]
    public void AddSchedule_WhenInvalidTimeZone_ShouldReturnFailure()
    {
        Job job = CreateJob();

        Result<Guid> result = job.AddSchedule("Daily", "0 0 10 * * ?", "Invalid/Timezone");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Job.InvalidTimeZone");
    }

    [Fact]
    public void AddSchedule_WhenInvalidTimeZone_ShouldNotAddScheduleOrRaiseEvent()
    {
        Job job = CreateJob();
        int countBefore = job.Schedules.Count;

        job.AddSchedule("Daily", "0 0 10 * * ?", "Invalid/Timezone");

        job.Schedules.Count.ShouldBe(countBefore);
        job.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void AddSchedule_MultipleCalls_ShouldAddAllSchedules()
    {
        Job job = CreateJob();
        int countBefore = job.Schedules.Count;

        job.AddSchedule("Daily", "0 0 10 * * ?", "UTC");
        job.AddSchedule("Weekly", "0 0 10 ? * MON", "UTC");

        job.Schedules.Count.ShouldBe(countBefore + 2);
    }

    // ── UpdateSchedule ────────────────────────────────────────────────────────

    [Fact]
    public void UpdateSchedule_ShouldUpdateScheduleProperties()
    {
        Job job = CreateJobWithSchedule(out Guid scheduleId);

        job.UpdateSchedule(scheduleId, "Weekly", "0 0 10 ? * MON", "UTC", false);

        JobSchedule schedule = job.Schedules.Single(s => s.Id == scheduleId);
        schedule.Name.ShouldBe("Weekly");
        schedule.CronExpression.ShouldBe("0 0 10 ? * MON");
        schedule.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void UpdateSchedule_ShouldRaiseJobScheduleUpdatedDomainEvent()
    {
        Job job = CreateJobWithSchedule(out Guid scheduleId);
        job.ClearDomainEvents();

        job.UpdateSchedule(scheduleId, "Weekly", "0 0 10 ? * MON", "UTC", true);

        job.DomainEvents.ShouldHaveSingleItem();
        var @event = job.DomainEvents[0].ShouldBeOfType<JobScheduleUpdatedDomainEvent>();
        @event.JobId.ShouldBe(job.Id);
        @event.ScheduleId.ShouldBe(scheduleId);
    }

    [Fact]
    public void UpdateSchedule_WhenScheduleNotFound_ShouldReturnFailure()
    {
        Job job = CreateJob();

        Result result = job.UpdateSchedule(Guid.NewGuid(), "Daily", "0 0 10 * * ?", "UTC", true);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Job.ScheduleNotFound");
    }

    [Fact]
    public void UpdateSchedule_WhenScheduleNotFound_ShouldNotRaiseEvent()
    {
        Job job = CreateJob();

        job.UpdateSchedule(Guid.NewGuid(), "Daily", "0 0 10 * * ?", "UTC", true);

        job.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void UpdateSchedule_WhenInvalidTimeZone_ShouldReturnFailure()
    {
        Job job = CreateJobWithSchedule(out Guid scheduleId);

        Result result = job.UpdateSchedule(scheduleId, "Daily", "0 0 10 * * ?", "Bad/Zone", true);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Job.InvalidTimeZone");
    }

    [Fact]
    public void UpdateSchedule_WhenInvalidTimeZone_ShouldNotMutateSchedule()
    {
        Job job = CreateJobWithSchedule(out Guid scheduleId);
        string originalName = job.Schedules.Single(s => s.Id == scheduleId).Name;

        job.UpdateSchedule(scheduleId, "New Name", "0 0 10 * * ?", "Bad/Zone", true);

        job.Schedules.Single(s => s.Id == scheduleId).Name.ShouldBe(originalName);
    }

    // ── RemoveSchedule ────────────────────────────────────────────────────────

    [Fact]
    public void RemoveSchedule_ShouldRemoveScheduleFromCollection()
    {
        Job job = CreateJobWithSchedule(out Guid scheduleId);
        int countBefore = job.Schedules.Count;

        job.RemoveSchedule(scheduleId);

        job.Schedules.Count.ShouldBe(countBefore - 1);
        job.Schedules.ShouldNotContain(s => s.Id == scheduleId);
    }

    [Fact]
    public void RemoveSchedule_ShouldRaiseJobScheduleDeletedDomainEvent()
    {
        Job job = CreateJobWithSchedule(out Guid scheduleId);
        job.ClearDomainEvents();

        job.RemoveSchedule(scheduleId);

        job.DomainEvents.ShouldHaveSingleItem();
        var @event = job.DomainEvents[0].ShouldBeOfType<JobScheduleDeletedDomainEvent>();
        @event.JobId.ShouldBe(job.Id);
        @event.ScheduleId.ShouldBe(scheduleId);
    }

    [Fact]
    public void RemoveSchedule_WhenScheduleNotFound_ShouldReturnFailure()
    {
        Job job = CreateJob();

        Result result = job.RemoveSchedule(Guid.NewGuid());

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Job.ScheduleNotFound");
    }

    [Fact]
    public void RemoveSchedule_WhenScheduleNotFound_ShouldNotRaiseEvent()
    {
        Job job = CreateJob();

        job.RemoveSchedule(Guid.NewGuid());

        job.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void RemoveSchedule_WithMultipleSchedules_ShouldOnlyRemoveTargeted()
    {
        Job job = CreateJob();
        Result<Guid> resultA = job.AddSchedule("Schedule A", "0 0 10 * * ?", "UTC");
        Result<Guid> resultB = job.AddSchedule("Schedule B", "0 0 12 * * ?", "UTC");
        job.ClearDomainEvents();

        job.RemoveSchedule(resultA.Value);

        job.Schedules.ShouldNotContain(s => s.Id == resultA.Value);
        job.Schedules.ShouldContain(s => s.Id == resultB.Value);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Job CreateJob() =>
        Job.Create(
            "Test Job",
            null,
            [("Step 1", "RunDatabaseLoad", null, OnFailureAction.Stop)],
            [("Daily", "0 0 10 * * ?", "UTC")]).Value;

    private static Job CreateJobWithSchedule(out Guid scheduleId)
    {
        Job job = CreateJob();
        scheduleId = job.Schedules[0].Id;
        return job;
    }
}
