using Application.Abstractions.Tenancy;
using Domain.Jobs;
using Infrastructure.Tenancy;
using SharedKernel;

namespace Worker.Scheduling;

// Handles all three schedule-change events from the outbox and re-syncs Quartz
// so changes made via the Web API take effect immediately — without requiring
// a Worker restart or waiting for the periodic ScheduleSyncService tick.
//
// TenantContext is already populated by DomainEventsDispatcher before invoking
// handlers, so ITenantContext carries the correct tenant for this event.
internal sealed class JobScheduleChangedHandler(
    TenantScheduleManager scheduleManager,
    ITenantContext tenantContext,
    ILogger<JobScheduleChangedHandler> logger)
    : IDomainEventHandler<JobScheduleAddedDomainEvent>,
      IDomainEventHandler<JobScheduleUpdatedDomainEvent>,
      IDomainEventHandler<JobScheduleDeletedDomainEvent>
{
    public Task Handle(JobScheduleAddedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        SyncAsync(domainEvent.ScheduleId, cancellationToken);

    public Task Handle(JobScheduleUpdatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        SyncAsync(domainEvent.ScheduleId, cancellationToken);

    public Task Handle(JobScheduleDeletedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        SyncAsync(domainEvent.ScheduleId, cancellationToken);

    private async Task SyncAsync(Guid scheduleId, CancellationToken cancellationToken)
    {
        TenantInfo tenant = new(tenantContext.TenantId, tenantContext.ConnectionString);

        logger.LogInformation(
            "Schedule {ScheduleId} changed for tenant {TenantId} — re-syncing Quartz triggers",
            scheduleId,
            tenant.TenantId);

        await scheduleManager.SyncTenantAsync(tenant, cancellationToken);
    }
}
