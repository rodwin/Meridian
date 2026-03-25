using Application.Abstractions.Messaging;

namespace Application.Jobs.Schedules.Add;

public sealed class AddJobScheduleCommand : ICommand<Guid>
{
    public Guid JobId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Quartz cron expression (6-7 fields, e.g. "0 0 10 * * ?").
    /// </summary>
    public string CronExpression { get; set; } = string.Empty;

    /// <summary>
    /// IANA or Windows timezone ID (e.g. "UTC", "New Zealand Standard Time").
    /// </summary>
    public string TimeZoneId { get; set; } = string.Empty;
}
