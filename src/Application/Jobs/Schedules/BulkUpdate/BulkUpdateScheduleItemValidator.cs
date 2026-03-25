using Application.Abstractions.Scheduling;
using FluentValidation;

namespace Application.Jobs.Schedules.BulkUpdate;

internal sealed class BulkUpdateScheduleItemValidator : AbstractValidator<BulkUpdateScheduleItem>
{
    public BulkUpdateScheduleItemValidator(ICronExpressionValidator cronValidator)
    {
        RuleFor(s => s.ScheduleId).NotEmpty();
        RuleFor(s => s.Name).NotEmpty().MaximumLength(200);
        RuleFor(s => s.CronExpression)
            .NotEmpty()
            .MaximumLength(100)
            .Must(cronValidator.IsValid)
            .WithMessage("Cron expression must be in Quartz format (e.g. '0 0 10 * * ?').");
        RuleFor(s => s.TimeZoneId).NotEmpty().MaximumLength(100);
        RuleFor(s => s.RowVersion).NotEmpty();
    }
}
