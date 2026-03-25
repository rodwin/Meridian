using Application.Abstractions.Messaging;
using Domain.Jobs;
using SharedKernel;

namespace Application.Jobs.Steps.BulkAdd;

public sealed class BulkAddJobStepsCommand : ICommand<BulkOperationResponse<StepResult>>
{
    public Guid JobId { get; set; }

    public List<BulkAddStepItem> Steps { get; set; } = [];
}

public sealed class BulkAddStepItem
{
    public string Name { get; set; } = string.Empty;

    public string StepType { get; set; } = string.Empty;

    public string? Parameters { get; set; }

    public OnFailureAction OnFailure { get; set; }
}

public sealed record StepResult(Guid Id, string Name, byte[] RowVersion);
