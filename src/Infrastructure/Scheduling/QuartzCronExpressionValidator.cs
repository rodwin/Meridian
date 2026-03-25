using Application.Abstractions.Scheduling;
using Quartz;

namespace Infrastructure.Scheduling;

internal sealed class QuartzCronExpressionValidator : ICronExpressionValidator
{
    public bool IsValid(string cronExpression) =>
        CronExpression.IsValidExpression(cronExpression);
}
