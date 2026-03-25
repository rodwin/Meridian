using Application.Abstractions.Messaging;

namespace Application.Jobs.Schedules.Update;

public sealed class UpdateJobScheduleCommand : ICommand
{
    public Guid JobId { get; set; }

    public Guid ScheduleId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string CronExpression { get; set; } = string.Empty;

    public string TimeZoneId { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
