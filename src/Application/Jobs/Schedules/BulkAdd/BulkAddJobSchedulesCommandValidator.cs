using FluentValidation;

namespace Application.Jobs.Schedules.BulkAdd;

internal sealed class BulkAddJobSchedulesCommandValidator : AbstractValidator<BulkAddJobSchedulesCommand>
{
    public BulkAddJobSchedulesCommandValidator()
    {
        RuleFor(c => c.JobId).NotEmpty();
        RuleFor(c => c.Schedules).NotEmpty();
    }
}
