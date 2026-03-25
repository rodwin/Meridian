using Infrastructure.Outbox;

namespace Worker.Messaging;

public sealed class JobMessage
{
    public string MessageType { get; init; } = string.Empty;  // 'DomainEvent' or 'ScheduledTrigger'
    public string QueueType { get; init; } = QueueTypes.Default;  // 'default' or 'longrunning'
    public string? EventType { get; init; }
    public string? JobType { get; init; }
    public string Payload { get; init; } = string.Empty;
    public string IdempotencyKey { get; init; } = string.Empty;
    public string? TraceParent { get; init; }
}
