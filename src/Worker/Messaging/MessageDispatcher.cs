using System.Diagnostics;
using Application.Abstractions.Messaging;
using Application.Jobs.Run;
using Infrastructure.DomainEvents;
using Infrastructure.Outbox;
using Infrastructure.Tenancy;
using Newtonsoft.Json;
using SharedKernel;

namespace Worker.Messaging;

internal sealed class MessageDispatcher(
    IServiceScopeFactory serviceScopeFactory,
    ITenantRegistry tenantRegistry,
    ILogger<MessageDispatcher> logger)
{
    private static readonly ActivitySource ActivitySource = new("Worker.MessageDispatcher");

    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        TypeNameHandling = TypeNameHandling.All
    };

    public async Task DispatchAsync(string tenantId, JobMessage message, CancellationToken cancellationToken)
    {
        string? connectionString = await tenantRegistry.GetConnectionStringAsync(tenantId);

        if (connectionString is null)
        {
            logger.LogError(
                "Tenant {TenantId} not found in registry — message {Id} dropped",
                tenantId,
                message.IdempotencyKey);
            return;
        }

        // Use Activity.Current (the SDK's ServiceBusProcessor span) as parent when available.
        // Fall back to the original request's context so the dispatch span stays in the same
        // trace rather than starting a new root (e.g. local dev with InMemoryJobQueue).
        ActivityContext parentContext = Activity.Current?.Context ?? default;

        if (parentContext == default &&
            message.TraceParent is not null &&
            ActivityContext.TryParse(message.TraceParent, null, isRemote: true, out ActivityContext parsedContext))
        {
            parentContext = parsedContext;
        }

        using Activity? activity = ActivitySource.StartActivity(
            $"dispatch {message.EventType ?? message.JobType}",
            ActivityKind.Consumer,
            parentContext: parentContext);

        activity?.SetTag("messaging.tenant_id", tenantId);
        activity?.SetTag("messaging.message_id", message.IdempotencyKey);

        await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();

        TenantContext tenantContext = scope.ServiceProvider.GetRequiredService<TenantContext>();
        tenantContext.TenantId = tenantId;
        tenantContext.ConnectionString = connectionString;

        if (message.MessageType == OutboxMessageTypes.DomainEvent)
        {
            IDomainEvent domainEvent = JsonConvert.DeserializeObject<IDomainEvent>(
                message.Payload,
                SerializerSettings)!;

            IDomainEventsDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventsDispatcher>();

            await dispatcher.DispatchAsync([domainEvent], cancellationToken);
        }
        else if (message.MessageType == OutboxMessageTypes.ScheduledTrigger)
        {
            RunJobCommand command = System.Text.Json.JsonSerializer.Deserialize<RunJobCommand>(message.Payload)!;

            ICommandHandler<RunJobCommand> handler =
                scope.ServiceProvider.GetRequiredService<ICommandHandler<RunJobCommand>>();

            await handler.Handle(command, cancellationToken);
        }
        else
        {
            logger.LogWarning(
                "Unknown message type '{MessageType}' — message {Id} dropped",
                message.MessageType,
                message.IdempotencyKey);
        }
    }
}
