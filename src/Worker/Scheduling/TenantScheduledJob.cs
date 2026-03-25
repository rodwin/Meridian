using System.Diagnostics;
using System.Text.Json;
using Infrastructure.Database;
using Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Quartz;

namespace Worker.Scheduling;

// Quartz IJob fired when a tenant's schedule trigger fires.
// Writes a ScheduledTrigger outbox message to the tenant's database so the
// existing OutboxProcessorService → ASB → MessageDispatcher pipeline delivers it.
// [DisallowConcurrentExecution] ensures one execution per schedule key at a time.
[DisallowConcurrentExecution]
internal sealed class TenantScheduledJob(
    ILogger<TenantScheduledJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        string tenantId = context.MergedJobDataMap.GetString("TenantId")!;
        string jobId = context.MergedJobDataMap.GetString("JobId")!;
        string scheduleId = context.MergedJobDataMap.GetString("ScheduleId")!;
        string connectionString = context.MergedJobDataMap.GetString("ConnectionString")!;

        logger.LogInformation(
            "Schedule trigger fired for tenant {TenantId}: job {JobId}, schedule {ScheduleId}",
            tenantId,
            jobId,
            scheduleId);

        // Build a context directly from the tenant connection string — same pattern
        // as OutboxProcessorService, which avoids needing the DI-scoped TenantContext.
        DbContextOptions<ApplicationDbContext> dbOptions =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(connectionString, sql =>
                    sql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.App))
                .Options;

        await using ApplicationDbContext dbContext = new(dbOptions);

        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            MessageType = OutboxMessageTypes.ScheduledTrigger,
            Type = OutboxMessageTypes.ScheduledTrigger,
            QueueType = QueueTypes.LongRunning,
            Payload = JsonSerializer.Serialize(new { JobId = jobId, ScheduleId = scheduleId }),
            OccurredOnUtc = DateTime.UtcNow,
            TraceParent = Activity.Current?.Id
        });

        await dbContext.SaveChangesAsync(context.CancellationToken);
    }
}
