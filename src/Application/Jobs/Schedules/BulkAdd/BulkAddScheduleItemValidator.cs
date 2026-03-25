using Application.Abstractions.Scheduling;
using FluentValidation;

namespace Application.Jobs.Schedules.BulkAdd;

internal sealed class BulkAddScheduleItemValidator : AbstractValidator<BulkAddScheduleItem>
{
    public BulkAddScheduleItemValidator(ICronExpressionValidator cronValidator)
    {
        RuleFor(s => s.Name).NotEmpty().MaximumLength(200);

        RuleFor(s => s.CronExpression)
            .NotEmpty()
            .MaximumLength(100)
            .Must(cronValidator.IsValid)
            .WithMessage("Cron expression must be in Quartz format (e.g. '0 0 10 * * ?').");

        RuleFor(s => s.TimeZoneId).NotEmpty().MaximumLength(100);
    }
}
