using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Jobs.Get;

internal sealed class GetJobQueryHandler(
    IApplicationDbContext context) : IQueryHandler<GetJobQuery, JobResponse>
{
    public async Task<Result<JobResponse>> Handle(GetJobQuery query, CancellationToken cancellationToken)
    {
        Job? job = await context.Jobs
            .Include(j => j.Schedules)
            .Include(j => j.Steps)
            .FirstOrDefaultAsync(j => j.Id == query.JobId, cancellationToken);

        if (job is null)
        {
            return Result.Failure<JobResponse>(JobErrors.NotFound(query.JobId));
        }

        return new JobResponse(
            job.Id,
            job.Name,
            job.Description,
            job.IsEnabled,
            job.Schedules
                .Select(s => new JobScheduleResponse(
                    s.Id,
                    s.Name,
                    s.CronExpression,
                    s.TimeZoneId,
                    s.IsEnabled,
                    s.RowVersion))
                .ToList(),
            job.Steps
                .OrderBy(s => s.StepOrder)
                .Select(s => new JobStepResponse(
                    s.Id,
                    s.StepOrder,
                    s.Name,
                    s.StepType,
                    s.Parameters,
                    s.OnFailure.ToString(),
                    s.IsEnabled,
                    s.RowVersion))
                .ToList());
    }
}
