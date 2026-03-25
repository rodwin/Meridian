using Application.Abstractions.Messaging;
using SharedKernel;

namespace Application.Jobs.Schedules.BulkAdd;

public sealed class BulkAddJobSchedulesCommand : ICommand<BulkOperationResponse<ScheduleResult>>
{
    public Guid JobId { get; set; }

    public List<BulkAddScheduleItem> Schedules { get; set; } = [];
}

public sealed class BulkAddScheduleItem
{
    public string Name { get; set; } = string.Empty;

    public string CronExpression { get; set; } = string.Empty;

    public string TimeZoneId { get; set; } = string.Empty;
}

public sealed record ScheduleResult(Guid Id, string Name, byte[] RowVersion);
