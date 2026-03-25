using SharedKernel;

namespace Domain.Jobs;

// Aggregate root. A job groups one or more ordered steps that run sequentially,
// triggered by one or more independent cron schedules — modelled after SQL Server Agent jobs.
public sealed class Job : Entity, IAuditableEntity
{
    private readonly List<JobStep> _steps = [];
    private readonly List<JobSchedule> _schedules = [];

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsEnabled { get; private set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public IReadOnlyList<JobStep> Steps => _steps;

    public IReadOnlyList<JobSchedule> Schedules => _schedules;

    // ── Factory ───────────────────────────────────────────────────────────────

    public static Result<Job> Create(
        string name,
        string? description,
        IReadOnlyList<(string Name, string StepType, string? Parameters, OnFailureAction OnFailure)> steps,
        IReadOnlyList<(string Name, string CronExpression, string TimeZoneId)> schedules)
    {
        if (steps.Count == 0)
        {
            return Result.Failure<Job>(JobErrors.NoSteps());
        }

        if (schedules.Count == 0)
        {
            return Result.Failure<Job>(JobErrors.NoSchedules());
        }

        foreach (var schedule in schedules)
        {
            if (!IsValidTimeZone(schedule.TimeZoneId))
            {
                return Result.Failure<Job>(JobErrors.InvalidTimeZone(schedule.TimeZoneId));
            }
        }

        var jobId = Guid.CreateVersion7();

        var job = new Job
        {
            Id = jobId,
            Name = name,
            Description = description,
            IsEnabled = true
        };

        for (int i = 0; i < steps.Count; i++)
        {
            var s = steps[i];
            job._steps.Add(JobStep.Create(jobId, i + 1, s.Name, s.StepType, s.Parameters, s.OnFailure));
        }

        foreach (var s in schedules)
        {
            job._schedules.Add(JobSchedule.Create(jobId, s.Name, s.CronExpression, s.TimeZoneId));
        }

        return Result.Success(job);
    }

    // ── Schedule management ───────────────────────────────────────────────────
    // All mutations go through the aggregate root so invariants are enforced
    // in one place and domain events are consistently raised.

    public Result<Guid> AddSchedule(string name, string cronExpression, string timeZoneId)
    {
        if (!IsValidTimeZone(timeZoneId))
        {
            return Result.Failure<Guid>(JobErrors.InvalidTimeZone(timeZoneId));
        }

        JobSchedule schedule = JobSchedule.Create(Id, name, cronExpression, timeZoneId);

        _schedules.Add(schedule);

        Raise(new JobScheduleAddedDomainEvent(Id, schedule.Id));

        return Result.Success(schedule.Id);
    }

    public Result UpdateSchedule(
        Guid scheduleId,
        string name,
        string cronExpression,
        string timeZoneId,
        bool isEnabled)
    {
        JobSchedule? schedule = _schedules.FirstOrDefault(s => s.Id == scheduleId);

        if (schedule is null)
        {
            return Result.Failure(JobErrors.ScheduleNotFound(scheduleId));
        }

        if (!IsValidTimeZone(timeZoneId))
        {
            return Result.Failure(JobErrors.InvalidTimeZone(timeZoneId));
        }

        schedule.Name = name;
        schedule.CronExpression = cronExpression;
        schedule.TimeZoneId = timeZoneId;
        schedule.IsEnabled = isEnabled;

        Raise(new JobScheduleUpdatedDomainEvent(Id, scheduleId));

        return Result.Success();
    }

    public Result RemoveSchedule(Guid scheduleId)
    {
        JobSchedule? schedule = _schedules.FirstOrDefault(s => s.Id == scheduleId);

        if (schedule is null)
        {
            return Result.Failure(JobErrors.ScheduleNotFound(scheduleId));
        }

        _schedules.Remove(schedule);

        Raise(new JobScheduleDeletedDomainEvent(Id, scheduleId));

        return Result.Success();
    }

    // ── Step management ─────────────────────────────────────────────────────

    public Result<Guid> AddStep(
        string name,
        string stepType,
        string? parameters,
        OnFailureAction onFailure)
    {
        int nextOrder = _steps.Count > 0 ? _steps.Max(s => s.StepOrder) + 1 : 1;

        JobStep step = JobStep.Create(Id, nextOrder, name, stepType, parameters, onFailure);

        _steps.Add(step);

        Raise(new JobStepAddedDomainEvent(Id, step.Id));

        return Result.Success(step.Id);
    }

    public Result UpdateStep(
        Guid stepId,
        string name,
        string stepType,
        string? parameters,
        OnFailureAction onFailure,
        bool isEnabled)
    {
        JobStep? step = _steps.FirstOrDefault(s => s.Id == stepId);

        if (step is null)
        {
            return Result.Failure(JobErrors.StepNotFound(stepId));
        }

        step.Name = name;
        step.StepType = stepType;
        step.Parameters = parameters;
        step.OnFailure = onFailure;
        step.IsEnabled = isEnabled;

        Raise(new JobStepUpdatedDomainEvent(Id, stepId));

        return Result.Success();
    }

    public Result RemoveStep(Guid stepId)
    {
        JobStep? step = _steps.FirstOrDefault(s => s.Id == stepId);

        if (step is null)
        {
            return Result.Failure(JobErrors.StepNotFound(stepId));
        }

        _steps.Remove(step);

        // Reorder remaining steps to keep sequence contiguous
        int order = 1;
        foreach (JobStep remaining in _steps.OrderBy(s => s.StepOrder))
        {
            remaining.StepOrder = order++;
        }

        Raise(new JobStepDeletedDomainEvent(Id, stepId));

        return Result.Success();
    }

    public Result ReorderSteps(IReadOnlyList<Guid> orderedStepIds)
    {
        if (orderedStepIds.Count != _steps.Count)
        {
            return Result.Failure(JobErrors.ReorderStepCountMismatch());
        }

        for (int i = 0; i < orderedStepIds.Count; i++)
        {
            JobStep? step = _steps.FirstOrDefault(s => s.Id == orderedStepIds[i]);

            if (step is null)
            {
                return Result.Failure(JobErrors.StepNotFound(orderedStepIds[i]));
            }

            step.StepOrder = i + 1;
        }

        return Result.Success();
    }

    private static bool IsValidTimeZone(string timeZoneId)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
    }
}
