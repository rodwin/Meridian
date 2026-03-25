using SharedKernel;

namespace Domain.Jobs;

public sealed record JobScheduleUpdatedDomainEvent(Guid JobId, Guid ScheduleId) : IDomainEvent;
