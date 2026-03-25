using SharedKernel;

namespace Domain.Jobs;

public sealed record JobStepDeletedDomainEvent(Guid JobId, Guid StepId) : IDomainEvent;
