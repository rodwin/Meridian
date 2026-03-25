using Application.Abstractions.Messaging;
using Domain.Jobs;
using SharedKernel;

namespace Application.Jobs.Steps.BulkUpdate;

public sealed class BulkUpdateJobStepsCommand : ICommand<BulkOperationResponse<StepResult>>
{
    public Guid JobId { get; set; }

    public List<BulkUpdateStepItem> Steps { get; set; } = [];
}

public sealed class BulkUpdateStepItem
{
    public Guid StepId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string StepType { get; set; } = string.Empty;

    public string? Parameters { get; set; }

    public OnFailureAction OnFailure { get; set; }

    public bool IsEnabled { get; set; }

    public byte[] RowVersion { get; set; } = [];
}

public sealed record StepResult(Guid Id, string Name, byte[] RowVersion);
