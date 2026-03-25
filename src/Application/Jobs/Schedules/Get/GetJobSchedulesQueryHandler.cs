using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Jobs.Get;
using Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Jobs.Schedules.Get;

internal sealed class GetJobSchedulesQueryHandler(
    IApplicationDbContext context) : IQueryHandler<GetJobSchedulesQuery, List<JobScheduleResponse>>
{
    public async Task<Result<List<JobScheduleResponse>>> Handle(GetJobSchedulesQuery query, CancellationToken cancellationToken)
    {
        bool jobExists = await context.Jobs
            .AnyAsync(j => j.Id == query.JobId, cancellationToken);

        if (!jobExists)
        {
            return Result.Failure<List<JobScheduleResponse>>(JobErrors.NotFound(query.JobId));
        }

        List<JobScheduleResponse> schedules = await context.Jobs
            .Where(j => j.Id == query.JobId)
            .SelectMany(j => j.Schedules)
            .Select(s => new JobScheduleResponse(
                s.Id,
                s.Name,
                s.CronExpression,
                s.TimeZoneId,
                s.IsEnabled,
                s.RowVersion))
            .ToListAsync(cancellationToken);

        return schedules;
    }
}
