using Application.Abstractions.Scheduling;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Scheduling;

internal sealed class ScheduledJobRepository(ApplicationDbContext dbContext) : IScheduledJobRepository
{
    // Returns one entry per enabled schedule across all enabled jobs.
    // The Worker maps each entry to a separate Quartz trigger so that
    // multiple schedules on the same job fire independently.
    public async Task<IReadOnlyList<ScheduledJobDto>> GetEnabledSchedulesAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Jobs
            .Where(j => j.IsEnabled)
            .SelectMany(j => j.Schedules
                .Where(s => s.IsEnabled)
                .Select(s => new ScheduledJobDto(j.Id, s.Id, s.CronExpression, s.TimeZoneId)))
            .ToListAsync(cancellationToken);
    }
}
