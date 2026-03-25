using SharedKernel;

namespace Domain.Jobs;

public static class JobErrors
{
    public static Error NotFound(Guid jobId) =>
        Error.NotFound("Job.NotFound", $"Job with id '{jobId}' was not found.");

    public static Error NoSteps() =>
        Error.Failure("Job.NoSteps", "A job must have at least one step.");

    public static Error NoSchedules() =>
        Error.Failure("Job.NoSchedules", "A job must have at least one schedule.");

    public static Error DuplicateStepOrder(int order) =>
        Error.Failure("Job.DuplicateStepOrder", $"Step order {order} appears more than once.");

    public static Error InvalidCronExpression(string expression) =>
        Error.Failure("Job.InvalidCronExpression", $"'{expression}' is not a valid cron expression.");

    public static Error InvalidTimeZone(string timeZoneId) =>
        Error.Failure("Job.InvalidTimeZone", $"'{timeZoneId}' is not a recognised timezone ID.");

    public static Error ScheduleNotFound(Guid scheduleId) =>
        Error.NotFound("Job.ScheduleNotFound", $"Schedule with id '{scheduleId}' was not found on this job.");

    public static Error StepNotFound(Guid stepId) =>
        Error.NotFound("Job.StepNotFound", $"Step with id '{stepId}' was not found on this job.");

    public static Error ReorderStepCountMismatch() =>
        Error.Failure("Job.ReorderStepCountMismatch", "The number of step IDs must match the number of steps on the job.");
}
