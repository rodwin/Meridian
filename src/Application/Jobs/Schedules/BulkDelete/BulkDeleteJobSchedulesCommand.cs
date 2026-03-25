using Application.Abstractions.Messaging;
using SharedKernel;

namespace Application.Jobs.Schedules.BulkDelete;

public sealed class BulkDeleteJobSchedulesCommand : ICommand<BulkOperationResponse<ScheduleDeleteResult>>
{
    public Guid JobId { get; set; }

    public List<BulkDeleteScheduleItem> Schedules { get; set; } = [];
}

public sealed class BulkDeleteScheduleItem
{
    public Guid ScheduleId { get; set; }

    public byte[] RowVersion { get; set; } = [];
}

public sealed record ScheduleDeleteResult(Guid Id);
