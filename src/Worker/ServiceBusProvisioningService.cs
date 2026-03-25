using Azure.Messaging.ServiceBus.Administration;
using Infrastructure.Tenancy;
using Microsoft.Extensions.Options;

namespace Worker;

// Ensures all ASB queues exist for every known tenant before the consumer starts.
//
// This runs as IHostedService (not BackgroundService) so its StartAsync completes
// and all queues are guaranteed to exist before ServiceBusConsumerService.ExecuteAsync
// begins attaching processors. Without this ordering, processors would fail
// immediately on startup if a queue was missing.
//
// This service handles bulk provisioning at startup only. Queues for tenants
// added at runtime are provisioned by ServiceBusConsumerService during its
// periodic reconciliation loop.
internal sealed class ServiceBusProvisioningService(
    ServiceBusAdministrationClient adminClient,
    ITenantRegistry tenantRegistry,
    IOptions<WorkerOptions> options,
    ILogger<ServiceBusProvisioningService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<TenantInfo> tenants = await tenantRegistry.GetAllAsync();

        logger.LogInformation("Provisioning ASB queues for {TenantCount} tenants", tenants.Count);

        foreach (TenantInfo tenant in tenants)
        {
            foreach (QueueTypeConfig queue in options.Value.Queues)
            {
                await EnsureQueueExistsAsync(tenant.TenantId, queue.Type, cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task EnsureQueueExistsAsync(
        string tenantId,
        string queueType,
        CancellationToken cancellationToken)
    {
        string queueName = $"{tenantId}-{queueType}";

        if (await adminClient.QueueExistsAsync(queueName, cancellationToken))
        {
            logger.LogDebug("ASB queue '{QueueName}' already exists, skipping", queueName);
            return;
        }

        await adminClient.CreateQueueAsync(TenantQueueFactory.BuildOptions(queueName), cancellationToken);

        logger.LogInformation("Created ASB queue '{QueueName}'", queueName);
    }
}
