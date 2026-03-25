using Application.Abstractions.Messaging;

namespace Application.Jobs.Get;

public sealed record GetJobQuery(Guid JobId) : IQuery<JobResponse>;

public sealed record JobResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsEnabled,
    List<JobScheduleResponse> Schedules,
    List<JobStepResponse> Steps);

public sealed record JobScheduleResponse(
    Guid Id,
    string Name,
    string CronExpression,
    string TimeZoneId,
    bool IsEnabled,
    byte[] RowVersion);

public sealed record JobStepResponse(
    Guid Id,
    int StepOrder,
    string Name,
    string StepType,
    string? Parameters,
    string OnFailure,
    bool IsEnabled,
    byte[] RowVersion);
