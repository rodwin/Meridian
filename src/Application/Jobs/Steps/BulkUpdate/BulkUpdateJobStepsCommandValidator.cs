using FluentValidation;

namespace Application.Jobs.Steps.BulkUpdate;

internal sealed class BulkUpdateJobStepsCommandValidator : AbstractValidator<BulkUpdateJobStepsCommand>
{
    public BulkUpdateJobStepsCommandValidator()
    {
        RuleFor(c => c.JobId).NotEmpty();
        RuleFor(c => c.Steps).NotEmpty();
    }
}
