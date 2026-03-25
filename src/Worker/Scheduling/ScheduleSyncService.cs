using Infrastructure.Tenancy;

namespace Worker.Scheduling;

// Periodically re-syncs all tenant schedules from the database into Quartz.
// This picks up any changes made through the Web API (cron updates, enable/disable,
// new schedules, deletions) without requiring a Worker restart.
//
// SyncTenantCoreAsync does a full replace: it deletes all existing Quartz triggers
// for the tenant then re-registers from the DB, so every kind of change is handled
// the same way.
internal sealed class ScheduleSyncService(
    TenantScheduleManager scheduleManager,
    ITenantRegistry tenantRegistry,
    ILogger<ScheduleSyncService> logger,
    TimeProvider timeProvider) : BackgroundService
{
    private static readonly TimeSpan SyncInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(SyncInterval, timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SyncAllTenantsAsync(stoppingToken);
        }
    }

    private async Task SyncAllTenantsAsync(CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<TenantInfo> tenants = await tenantRegistry.GetAllAsync();

            await Task.WhenAll(tenants.Select(t => SyncTenantSafeAsync(t, cancellationToken)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load tenant list during schedule sync");
        }
    }

    private async Task SyncTenantSafeAsync(TenantInfo tenant, CancellationToken cancellationToken)
    {
        try
        {
            await scheduleManager.SyncTenantAsync(tenant, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync schedules for tenant {TenantId}", tenant.TenantId);
        }
    }
}
