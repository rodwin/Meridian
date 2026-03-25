namespace Application.Abstractions.Scheduling;

public interface ICronExpressionValidator
{
    bool IsValid(string cronExpression);
}
