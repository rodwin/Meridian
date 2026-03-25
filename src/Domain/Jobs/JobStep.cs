using SharedKernel;

namespace Domain.Jobs;

public sealed class JobStep : IAuditableEntity
{
    public Guid Id { get; private set; }

    public Guid JobId { get; private set; }

    // 1-based execution order within the job.
    public int StepOrder { get; internal set; }

    public string Name { get; internal set; } = string.Empty;

    // Identifies which command handler executes this step (e.g. "RunDatabaseLoad").
    public string StepType { get; internal set; } = string.Empty;

    // JSON-serialised configuration specific to the StepType.
    public string? Parameters { get; internal set; }

    public OnFailureAction OnFailure { get; internal set; }

    public bool IsEnabled { get; internal set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = [];

    internal static JobStep Create(
        Guid jobId,
        int stepOrder,
        string name,
        string stepType,
        string? parameters,
        OnFailureAction onFailure)
    {
        return new JobStep
        {
            Id = Guid.CreateVersion7(),
            JobId = jobId,
            StepOrder = stepOrder,
            Name = name,
            StepType = stepType,
            Parameters = parameters,
            OnFailure = onFailure,
            IsEnabled = true
        };
    }
}
