using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Jobs.Run;

internal sealed class RunJobCommandHandler(
    IApplicationDbContext context,
    ILogger<RunJobCommandHandler> logger) : ICommandHandler<RunJobCommand>
{
    public async Task<Result> Handle(RunJobCommand command, CancellationToken cancellationToken)
    {
        Domain.Jobs.Job? job = await context.Jobs
            .Include(j => j.Steps)
            .Include(j => j.Schedules)
            .FirstOrDefaultAsync(j => j.Id == command.JobId, cancellationToken);

        if (job is null)
        {
            logger.LogWarning(
                "Job {JobId} not found — scheduled trigger from schedule {ScheduleId} will be dropped",
                command.JobId,
                command.ScheduleId);

            return Result.Failure(Domain.Jobs.JobErrors.NotFound(command.JobId));
        }

        logger.LogInformation(
            "Running job '{JobName}' ({JobId}) triggered by schedule {ScheduleId} with {StepCount} step(s)",
            job.Name,
            command.JobId,
            command.ScheduleId,
            job.Steps.Count);

        return Result.Success();
    }
}
