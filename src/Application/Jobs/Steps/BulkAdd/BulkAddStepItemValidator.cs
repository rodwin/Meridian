using FluentValidation;

namespace Application.Jobs.Steps.BulkAdd;

internal sealed class BulkAddStepItemValidator : AbstractValidator<BulkAddStepItem>
{
    public BulkAddStepItemValidator()
    {
        RuleFor(s => s.Name).NotEmpty().MaximumLength(200);
        RuleFor(s => s.StepType).NotEmpty().MaximumLength(100);
        RuleFor(s => s.OnFailure).IsInEnum();
    }
}
