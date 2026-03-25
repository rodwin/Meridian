namespace Worker.IntegrationTests.TestDoubles;

/// <summary>
/// Always throws to simulate Service Bus being unavailable.
/// Used to verify the outbox relay resets the claim so the next poll can retry.
/// </summary>
internal sealed class FailingJobQueue : IJobQueue
{
    public Task EnqueueAsync(string tenantId, JobMessage message, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Simulated Service Bus failure");
}
