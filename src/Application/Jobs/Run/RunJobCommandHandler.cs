using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Jobs.Run;

internal sealed partial class RunJobCommandHandler(
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
            LogJobNotFound(logger, command.JobId, command.ScheduleId);
            return Result.Failure(Domain.Jobs.JobErrors.NotFound(command.JobId));
        }

        LogRunningJob(logger, job.Name, command.JobId, command.ScheduleId, job.Steps.Count);

        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Job {JobId} not found — scheduled trigger from schedule {ScheduleId} will be dropped")]
    private static partial void LogJobNotFound(ILogger logger, Guid jobId, Guid scheduleId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Running job '{JobName}' ({JobId}) triggered by schedule {ScheduleId} with {StepCount} step(s)")]
    private static partial void LogRunningJob(ILogger logger, string jobName, Guid jobId, Guid scheduleId, int stepCount);
}
