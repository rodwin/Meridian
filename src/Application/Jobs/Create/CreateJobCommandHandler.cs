using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Jobs;
using SharedKernel;

namespace Application.Jobs.Create;

internal sealed class CreateJobCommandHandler(
    IApplicationDbContext context) : ICommandHandler<CreateJobCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateJobCommand command, CancellationToken cancellationToken)
    {
        Result<Job> result = Job.Create(
            command.Name,
            command.Description,
            command.Steps
                .Select(s => (s.Name, s.StepType, s.Parameters, s.OnFailure))
                .ToArray(),
            command.Schedules
                .Select(s => (s.Name, s.CronExpression, s.TimeZoneId))
                .ToArray());

        if (result.IsFailure)
        {
            return Result.Failure<Guid>(result.Error);
        }

        context.Jobs.Add(result.Value);

        await context.SaveChangesAsync(cancellationToken);

        return result.Value.Id;
    }
}
