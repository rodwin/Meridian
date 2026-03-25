using Application.Abstractions.Scheduling;

namespace Application.IntegrationTests.Fixtures;

/// <summary>
/// Test stub that treats every cron expression as valid.
/// Use this when the test cares about handler/domain behaviour, not cron parsing.
/// </summary>
internal sealed class AlwaysValidCronValidator : ICronExpressionValidator
{
    public static readonly AlwaysValidCronValidator Instance = new();

    public bool IsValid(string? cronExpression) => true;
}
