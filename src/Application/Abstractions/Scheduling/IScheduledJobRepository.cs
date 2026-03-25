namespace Application.Abstractions.Scheduling;

public interface IScheduledJobRepository
{
    // Returns one ScheduledJobDto per enabled schedule across all enabled jobs.
    // Used by the Worker to rebuild Quartz triggers on startup and after syncs.
    Task<IReadOnlyList<ScheduledJobDto>> GetEnabledSchedulesAsync(CancellationToken cancellationToken = default);
}
