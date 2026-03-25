using FluentValidation;

namespace Application.Jobs.Steps.Reorder;

internal sealed class ReorderJobStepsCommandValidator : AbstractValidator<ReorderJobStepsCommand>
{
    public ReorderJobStepsCommandValidator()
    {
        RuleFor(c => c.JobId).NotEmpty();
        RuleFor(c => c.StepIds).NotEmpty();
    }
}
