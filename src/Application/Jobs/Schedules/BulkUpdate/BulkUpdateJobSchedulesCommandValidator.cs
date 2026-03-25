using FluentValidation;

namespace Application.Jobs.Schedules.BulkUpdate;

internal sealed class BulkUpdateJobSchedulesCommandValidator : AbstractValidator<BulkUpdateJobSchedulesCommand>
{
    public BulkUpdateJobSchedulesCommandValidator()
    {
        RuleFor(c => c.JobId).NotEmpty();
        RuleFor(c => c.Schedules).NotEmpty();
    }
}
