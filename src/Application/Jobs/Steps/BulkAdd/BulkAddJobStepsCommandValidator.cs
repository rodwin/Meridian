using FluentValidation;

namespace Application.Jobs.Steps.BulkAdd;

internal sealed class BulkAddJobStepsCommandValidator : AbstractValidator<BulkAddJobStepsCommand>
{
    public BulkAddJobStepsCommandValidator()
    {
        RuleFor(c => c.JobId).NotEmpty();
        RuleFor(c => c.Steps).NotEmpty();
    }
}
