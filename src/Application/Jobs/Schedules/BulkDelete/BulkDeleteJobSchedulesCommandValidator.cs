using FluentValidation;

namespace Application.Jobs.Schedules.BulkDelete;

internal sealed class BulkDeleteJobSchedulesCommandValidator : AbstractValidator<BulkDeleteJobSchedulesCommand>
{
    public BulkDeleteJobSchedulesCommandValidator()
    {
        RuleFor(c => c.JobId).NotEmpty();
        RuleFor(c => c.Schedules).NotEmpty();
    }
}
