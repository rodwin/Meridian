namespace Application.Abstractions.Scheduling;

// Represents one enabled schedule entry the Worker loads into Quartz.
// Each JobSchedule in the DB becomes one Quartz trigger; ScheduleId
// is used as the Quartz job key so the scheduler can target it precisely.
public sealed record ScheduledJobDto(
    Guid JobId,
    Guid ScheduleId,
    string CronExpression,
    string TimeZoneId);
