using Application.Abstractions.Messaging;

namespace Application.Jobs.Schedules.Delete;

public sealed class DeleteJobScheduleCommand : ICommand
{
    public Guid JobId { get; set; }

    public Guid ScheduleId { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
