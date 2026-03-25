using SharedKernel;

namespace Domain.Jobs;

public sealed record JobStepUpdatedDomainEvent(Guid JobId, Guid StepId) : IDomainEvent;
