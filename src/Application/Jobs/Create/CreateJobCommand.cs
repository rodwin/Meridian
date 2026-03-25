using Application.Abstractions.Messaging;
using Domain.Jobs;

namespace Application.Jobs.Create;

public sealed class CreateJobCommand : ICommand<Guid>
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<CreateJobStepRequest> Steps { get; set; } = [];

    public List<CreateJobScheduleRequest> Schedules { get; set; } = [];
}

public sealed class CreateJobStepRequest
{
    public string Name { get; set; } = string.Empty;

    // Maps to a command handler registered in the Worker (e.g. "RunDatabaseLoad").
    public string StepType { get; set; } = string.Empty;

    // JSON-serialised configuration passed to the step handler at runtime.
    public string? Parameters { get; set; }

    public OnFailureAction OnFailure { get; set; }
}

public sealed class CreateJobScheduleRequest
{
    public string Name { get; set; } = string.Empty;

    public string CronExpression { get; set; } = string.Empty;

    // IANA or Windows timezone ID (e.g. "UTC", "New Zealand Standard Time").
    public string TimeZoneId { get; set; } = string.Empty;
}
