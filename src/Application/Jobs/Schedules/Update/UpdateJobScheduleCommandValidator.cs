using Application.Abstractions.Scheduling;
using FluentValidation;

namespace Application.Jobs.Schedules.Update;

internal sealed class UpdateJobScheduleCommandValidator : AbstractValidator<UpdateJobScheduleCommand>
{
    public UpdateJobScheduleCommandValidator(ICronExpressionValidator cronValidator)
    {
        RuleFor(c => c.JobId).NotEmpty();

        RuleFor(c => c.ScheduleId).NotEmpty();

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(c => c.CronExpression)
            .NotEmpty()
            .MaximumLength(100)
            .Must(cronValidator.IsValid)
            .WithMessage("Cron expression must be in Quartz format (e.g. '0 0 10 * * ?').");

        RuleFor(c => c.TimeZoneId)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(c => c.RowVersion).NotEmpty();
    }
}
