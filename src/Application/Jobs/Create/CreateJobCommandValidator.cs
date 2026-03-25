using Application.Abstractions.Scheduling;
using FluentValidation;

namespace Application.Jobs.Create;

internal sealed class CreateJobCommandValidator : AbstractValidator<CreateJobCommand>
{
    public CreateJobCommandValidator(ICronExpressionValidator cronValidator)
    {
        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(c => c.Description)
            .MaximumLength(1000)
            .When(c => c.Description is not null);

        RuleFor(c => c.Steps)
            .NotEmpty()
            .WithMessage("A job must have at least one step.");

        RuleForEach(c => c.Steps).SetValidator(new StepValidator());

        RuleFor(c => c.Schedules)
            .NotEmpty()
            .WithMessage("A job must have at least one schedule.");

        RuleForEach(c => c.Schedules).SetValidator(new ScheduleValidator(cronValidator));
    }

    private sealed class StepValidator : AbstractValidator<CreateJobStepRequest>
    {
        public StepValidator()
        {
            RuleFor(s => s.Name).NotEmpty().MaximumLength(200);
            RuleFor(s => s.StepType).NotEmpty().MaximumLength(100);
            RuleFor(s => s.OnFailure).IsInEnum();
        }
    }

    private sealed class ScheduleValidator : AbstractValidator<CreateJobScheduleRequest>
    {
        public ScheduleValidator(ICronExpressionValidator cronValidator)
        {
            RuleFor(s => s.Name).NotEmpty().MaximumLength(200);

            RuleFor(s => s.CronExpression)
                .NotEmpty()
                .MaximumLength(100)
                .Must(cronValidator.IsValid)
                .WithMessage("Cron expression must be in Quartz format (e.g. '0 0 10 * * ?').");

            RuleFor(s => s.TimeZoneId)
                .NotEmpty()
                .MaximumLength(100)
                .Must(BeValidTimeZone)
                .WithMessage("'{PropertyValue}' is not a recognised timezone ID.");
        }

        private static bool BeValidTimeZone(string timeZoneId)
        {
            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                return true;
            }
            catch (TimeZoneNotFoundException)
            {
                return false;
            }
        }
    }
}
