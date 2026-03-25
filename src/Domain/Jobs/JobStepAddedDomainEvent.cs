using SharedKernel;

namespace Domain.Jobs;

public sealed record JobStepAddedDomainEvent(Guid JobId, Guid StepId) : IDomainEvent;
