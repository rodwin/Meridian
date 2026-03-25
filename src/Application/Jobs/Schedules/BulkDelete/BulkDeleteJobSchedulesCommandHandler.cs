using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Jobs.Schedules.BulkDelete;

internal sealed class BulkDeleteJobSchedulesCommandHandler(
    IApplicationDbContext context) : ICommandHandler<BulkDeleteJobSchedulesCommand, BulkOperationResponse<ScheduleDeleteResult>>
{
    public async Task<Result<BulkOperationResponse<ScheduleDeleteResult>>> Handle(
        BulkDeleteJobSchedulesCommand command,
        CancellationToken cancellationToken)
    {
        Job? job = await context.Jobs
            .Include(j => j.Schedules)
            .FirstOrDefaultAsync(j => j.Id == command.JobId, cancellationToken);

        if (job is null)
        {
            return Result.Failure<BulkOperationResponse<ScheduleDeleteResult>>(
                JobErrors.NotFound(command.JobId));
        }

        var succeeded = new List<ScheduleDeleteResult>();
        var failed = new List<BulkItemFailure>();

        for (int i = 0; i < command.Schedules.Count; i++)
        {
            BulkDeleteScheduleItem item = command.Schedules[i];

            JobSchedule? schedule = job.Schedules.FirstOrDefault(s => s.Id == item.ScheduleId);
            if (schedule is not null)
            {
                context.Entry(schedule).Property(s => s.RowVersion).OriginalValue = item.RowVersion;
            }

            Result result = job.RemoveSchedule(item.ScheduleId);

            if (result.IsFailure)
            {
                failed.Add(new BulkItemFailure(
                    i, item.ScheduleId, null,
                    [result.Error.Description]));
                continue;
            }

            succeeded.Add(new ScheduleDeleteResult(item.ScheduleId));
        }

        if (succeeded.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return new BulkOperationResponse<ScheduleDeleteResult>(succeeded, failed);
    }
}
