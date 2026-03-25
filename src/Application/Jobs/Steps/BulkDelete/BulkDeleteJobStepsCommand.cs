using Application.Abstractions.Messaging;
using SharedKernel;

namespace Application.Jobs.Steps.BulkDelete;

public sealed class BulkDeleteJobStepsCommand : ICommand<BulkOperationResponse<StepDeleteResult>>
{
    public Guid JobId { get; set; }

    public List<BulkDeleteStepItem> Steps { get; set; } = [];
}

public sealed class BulkDeleteStepItem
{
    public Guid StepId { get; set; }

    public byte[] RowVersion { get; set; } = [];
}

public sealed record StepDeleteResult(Guid Id);
