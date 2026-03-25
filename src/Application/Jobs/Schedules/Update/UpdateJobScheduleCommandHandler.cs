using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Jobs.Schedules.Update;

internal sealed class UpdateJobScheduleCommandHandler(
    IApplicationDbContext context) : ICommandHandler<UpdateJobScheduleCommand>
{
    public async Task<Result> Handle(UpdateJobScheduleCommand command, CancellationToken cancellationToken)
    {
        Job? job = await context.Jobs
            .Include(j => j.Schedules)
            .FirstOrDefaultAsync(j => j.Id == command.JobId, cancellationToken);

        if (job is null)
        {
            return Result.Failure(JobErrors.NotFound(command.JobId));
        }

        Result result = job.UpdateSchedule(
            command.ScheduleId,
            command.Name,
            command.CronExpression,
            command.TimeZoneId,
            command.IsEnabled);

        if (result.IsFailure)
        {
            return result;
        }

        JobSchedule schedule = job.Schedules.First(s => s.Id == command.ScheduleId);
        context.Entry(schedule).Property(s => s.RowVersion).OriginalValue = command.RowVersion;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
