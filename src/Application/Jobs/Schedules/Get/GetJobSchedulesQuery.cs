using Application.Abstractions.Messaging;
using Application.Jobs.Get;

namespace Application.Jobs.Schedules.Get;

public sealed record GetJobSchedulesQuery(Guid JobId) : IQuery<List<JobScheduleResponse>>;
