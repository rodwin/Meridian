using FluentValidation;

namespace Application.Jobs.Steps.BulkDelete;

internal sealed class BulkDeleteJobStepsCommandValidator : AbstractValidator<BulkDeleteJobStepsCommand>
{
    public BulkDeleteJobStepsCommandValidator()
    {
        RuleFor(c => c.JobId).NotEmpty();
        RuleFor(c => c.Steps).NotEmpty();
    }
}
