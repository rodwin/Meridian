using FluentValidation;

namespace Application.Jobs.Steps.BulkUpdate;

internal sealed class BulkUpdateStepItemValidator : AbstractValidator<BulkUpdateStepItem>
{
    public BulkUpdateStepItemValidator()
    {
        RuleFor(s => s.StepId).NotEmpty();
        RuleFor(s => s.Name).NotEmpty().MaximumLength(200);
        RuleFor(s => s.StepType).NotEmpty().MaximumLength(100);
        RuleFor(s => s.OnFailure).IsInEnum();
        RuleFor(s => s.RowVersion).NotEmpty();
    }
}
