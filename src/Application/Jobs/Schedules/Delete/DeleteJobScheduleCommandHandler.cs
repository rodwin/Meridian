using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Jobs.Schedules.Delete;

internal sealed class DeleteJobScheduleCommandHandler(
    IApplicationDbContext context) : ICommandHandler<DeleteJobScheduleCommand>
{
    public async Task<Result> Handle(DeleteJobScheduleCommand command, CancellationToken cancellationToken)
    {
        Job? job = await context.Jobs
            .Include(j => j.Schedules)
            .FirstOrDefaultAsync(j => j.Id == command.JobId, cancellationToken);

        if (job is null)
        {
            return Result.Failure(JobErrors.NotFound(command.JobId));
        }

        JobSchedule? schedule = job.Schedules.FirstOrDefault(s => s.Id == command.ScheduleId);
        if (schedule is not null)
        {
            context.Entry(schedule).Property(s => s.RowVersion).OriginalValue = command.RowVersion;
        }

        Result result = job.RemoveSchedule(command.ScheduleId);

        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
