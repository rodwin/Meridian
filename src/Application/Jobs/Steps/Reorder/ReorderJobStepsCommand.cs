using Application.Abstractions.Messaging;

namespace Application.Jobs.Steps.Reorder;

public sealed class ReorderJobStepsCommand : ICommand
{
    public Guid JobId { get; set; }

    public List<Guid> StepIds { get; set; } = [];
}
