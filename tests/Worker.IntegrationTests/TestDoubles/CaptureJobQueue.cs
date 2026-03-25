namespace Worker.IntegrationTests.TestDoubles;

/// <summary>
/// Records every enqueue call so tests can assert which messages were relayed
/// and with what data, without needing a real Azure Service Bus connection.
/// </summary>
internal sealed class CaptureJobQueue : IJobQueue
{
    public List<(string TenantId, JobMessage Message)> Captured { get; } = [];

    public Task EnqueueAsync(string tenantId, JobMessage message, CancellationToken cancellationToken = default)
    {
        Captured.Add((tenantId, message));
        return Task.CompletedTask;
    }
}
