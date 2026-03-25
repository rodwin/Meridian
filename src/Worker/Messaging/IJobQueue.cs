namespace Worker.Messaging;

public interface IJobQueue
{
    Task EnqueueAsync(string tenantId, JobMessage message, CancellationToken cancellationToken = default);
}
