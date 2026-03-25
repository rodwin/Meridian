namespace Worker.Scheduling;

// Loads all tenant scheduled jobs into Quartz on worker startup.
// Implemented as IHostedService (not BackgroundService) so StartAsync completes
// before the next hosted service starts — Quartz itself must be running first,
// which is guaranteed by registering AddQuartzHostedService before this service.
internal sealed class ScheduleLoaderService(
    TenantScheduleManager scheduleManager,
    ILogger<ScheduleLoaderService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Loading tenant schedules into Quartz");

        await scheduleManager.LoadAllAsync(cancellationToken);

        logger.LogInformation("Tenant schedule load complete");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
