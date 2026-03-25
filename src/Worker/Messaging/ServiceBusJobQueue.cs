using Azure.Messaging.ServiceBus;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Worker.Messaging;

// Publishes to per-tenant ASB queues (tenant-{tenantId}).
// Used in production / when Worker:UseLocalJobQueue = false.
internal sealed class ServiceBusJobQueue(ServiceBusClient client) : IJobQueue, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new();

    public async Task EnqueueAsync(string tenantId, JobMessage message, CancellationToken cancellationToken = default)
    {
        string queueName = $"{tenantId}-{message.QueueType}";
        ServiceBusSender sender = _senders.GetOrAdd(queueName, client.CreateSender);

        ServiceBusMessage sbMessage = new(JsonSerializer.Serialize(message))
        {
            MessageId = message.IdempotencyKey,  // ASB dedup key
            ContentType = "application/json"
        };

        // SDK automatically injects Activity.Current's traceparent into ApplicationProperties
        // on send, and reads it on receive to parent the process span.
        await sender.SendMessageAsync(sbMessage, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (ServiceBusSender sender in _senders.Values)
        {
            await sender.DisposeAsync();
        }
    }
}
