using SharedKernel;

namespace Domain.Jobs;

public sealed record JobScheduleDeletedDomainEvent(Guid JobId, Guid ScheduleId) : IDomainEvent;
