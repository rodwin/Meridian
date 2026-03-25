using Application.Abstractions.Scheduling;
using Infrastructure.Tenancy;
using Quartz;
using Quartz.Impl.Matchers;

namespace Worker.Scheduling;

// Bridges the tenant registry and Quartz's in-memory scheduler.
// On startup, LoadAllAsync reads every active tenant's enabled schedules
// and registers one Quartz trigger per schedule. SyncTenantAsync can be
// called at runtime whenever a tenant's schedule changes.
//
// Tenant databases are the source of truth — Quartz holds no persistent state.
// On pod restart, LoadAllAsync rebuilds the full trigger set from the DB.
internal sealed class TenantScheduleManager(
    ISchedulerFactory schedulerFactory,
    ITenantRegistry tenantRegistry,
    IServiceScopeFactory scopeFactory,
    ILogger<TenantScheduleManager> logger)
{
    public async Task LoadAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TenantInfo> tenants = await tenantRegistry.GetAllAsync();

        logger.LogInformation("Loading Quartz schedules for {Count} tenant(s)", tenants.Count);

        IScheduler scheduler = await schedulerFactory.GetScheduler(cancellationToken);

        foreach (TenantInfo tenant in tenants)
        {
            await SyncTenantCoreAsync(scheduler, tenant, cancellationToken);
        }
    }

    public async Task SyncTenantAsync(TenantInfo tenant, CancellationToken cancellationToken = default)
    {
        IScheduler scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        await SyncTenantCoreAsync(scheduler, tenant, cancellationToken);
    }

    private async Task SyncTenantCoreAsync(
        IScheduler scheduler,
        TenantInfo tenant,
        CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<ScheduledJobDto> schedules = await GetSchedulesForTenantAsync(tenant, cancellationToken);

            // Remove all existing Quartz triggers for this tenant before re-registering.
            // Handles jobs/schedules that were disabled or deleted since the last sync.
            IReadOnlyCollection<JobKey> existingKeys = await scheduler.GetJobKeys(
                GroupMatcher<JobKey>.GroupEquals(tenant.TenantId),
                cancellationToken);

            foreach (JobKey key in existingKeys)
            {
                await scheduler.DeleteJob(key, cancellationToken);
            }

            // Each JobSchedule becomes one independent Quartz job+trigger.
            int registered = 0;
            foreach (ScheduledJobDto schedule in schedules)
            {
                if (await RegisterScheduleAsync(scheduler, tenant, schedule, cancellationToken))
                {
                    registered++;
                }
            }

            logger.LogInformation(
                "Synced {Registered}/{Total} schedule trigger(s) for tenant {TenantId}",
                registered,
                schedules.Count,
                tenant.TenantId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync Quartz schedules for tenant {TenantId}", tenant.TenantId);
        }
    }

    private async Task<bool> RegisterScheduleAsync(
        IScheduler scheduler,
        TenantInfo tenant,
        ScheduledJobDto schedule,
        CancellationToken cancellationToken)
    {
        if (!CronExpression.IsValidExpression(schedule.CronExpression))
        {
            logger.LogWarning(
                "Skipping schedule {ScheduleId} for tenant {TenantId}: " +
                "'{CronExpression}' is not a valid Quartz cron expression (needs 6-7 fields, e.g. '0 0 10 ? * *')",
                schedule.ScheduleId,
                tenant.TenantId,
                schedule.CronExpression);
            return false;
        }

        // Quartz group = tenantId so GetJobKeys(GroupEquals(tenantId)) finds all triggers for a tenant.
        // Quartz name = scheduleId so each JobSchedule maps to exactly one Quartz job.
        var jobKey = new JobKey(schedule.ScheduleId.ToString(), tenant.TenantId);

        IJobDetail jobDetail = JobBuilder.Create<TenantScheduledJob>()
            .WithIdentity(jobKey)
            .UsingJobData("TenantId", tenant.TenantId)
            .UsingJobData("ConnectionString", tenant.ConnectionString)
            .UsingJobData("JobId", schedule.JobId.ToString())
            .UsingJobData("ScheduleId", schedule.ScheduleId.ToString())
            .Build();

        TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZoneId);

        ITrigger trigger = TriggerBuilder.Create()
            .WithCronSchedule(schedule.CronExpression, x => x.InTimeZone(tz))
            .Build();

        DateTimeOffset nextFireTime = await scheduler.ScheduleJob(jobDetail, trigger, cancellationToken);

        logger.LogInformation(
            "Scheduled trigger for schedule {ScheduleId} (tenant {TenantId}), next fire: {NextFireTime}",
            schedule.ScheduleId,
            tenant.TenantId,
            nextFireTime.ToLocalTime());

        return true;
    }

    private async Task<IReadOnlyList<ScheduledJobDto>> GetSchedulesForTenantAsync(
        TenantInfo tenant,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

        // Populate TenantContext so ApplicationDbContext connects to the right tenant DB.
        TenantContext tenantContext = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantContext.TenantId = tenant.TenantId;
        tenantContext.ConnectionString = tenant.ConnectionString;

        IScheduledJobRepository repository =
            scope.ServiceProvider.GetRequiredService<IScheduledJobRepository>();

        return await repository.GetEnabledSchedulesAsync(cancellationToken);
    }
}
