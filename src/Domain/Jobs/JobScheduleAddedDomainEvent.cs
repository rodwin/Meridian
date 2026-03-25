using SharedKernel;

namespace Domain.Jobs;

public sealed record JobScheduleAddedDomainEvent(Guid JobId, Guid ScheduleId) : IDomainEvent;
