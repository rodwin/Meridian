using Application.Abstractions.Messaging;
using SharedKernel;

namespace Application.Jobs.Schedules.BulkUpdate;

public sealed class BulkUpdateJobSchedulesCommand : ICommand<BulkOperationResponse<ScheduleResult>>
{
    public Guid JobId { get; set; }

    public List<BulkUpdateScheduleItem> Schedules { get; set; } = [];
}

public sealed class BulkUpdateScheduleItem
{
    public Guid ScheduleId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string CronExpression { get; set; } = string.Empty;

    public string TimeZoneId { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public byte[] RowVersion { get; set; } = [];
}

public sealed record ScheduleResult(Guid Id, string Name, byte[] RowVersion);
