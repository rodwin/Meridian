using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Jobs.Schedules.Add;

internal sealed class AddJobScheduleCommandHandler(
    IApplicationDbContext context) : ICommandHandler<AddJobScheduleCommand, Guid>
{
    public async Task<Result<Guid>> Handle(AddJobScheduleCommand command, CancellationToken cancellationToken)
    {
        Job? job = await context.Jobs
            .Include(j => j.Schedules)
            .FirstOrDefaultAsync(j => j.Id == command.JobId, cancellationToken);

        if (job is null)
        {
            return Result.Failure<Guid>(JobErrors.NotFound(command.JobId));
        }

        Result<Guid> result = job.AddSchedule(command.Name, command.CronExpression, command.TimeZoneId);

        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return result;
    }
}
