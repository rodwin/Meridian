namespace Worker.Messaging;

// Used for local development (Worker:UseLocalJobQueue = true).
// Dispatches messages inline — no ASB dependency needed.
internal sealed class InMemoryJobQueue(MessageDispatcher dispatcher) : IJobQueue
{
    public Task EnqueueAsync(string tenantId, JobMessage message, CancellationToken cancellationToken = default) =>
        dispatcher.DispatchAsync(tenantId, message, cancellationToken);
}
