namespace Infrastructure.Outbox;

public sealed record OutboxMessage
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string MessageType { get; init; } = string.Empty;
    public string QueueType { get; init; } = QueueTypes.Default;
    public string Type { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty;
    public string? TraceParent { get; init; }
    public DateTime OccurredOnUtc { get; init; }
    public bool IsRelayed { get; set; }
    public DateTime? RelayedAt { get; set; }
    public string? Error { get; set; }

    // Set when a relay worker claims this message for processing.
    // Allows the relay loop to publish to the job queue outside a DB transaction
    // while still preventing concurrent workers from picking up the same row.
    // Cleared on success or failure — the reaper resets rows stuck here due to a crash.
    public DateTime? ProcessingStartedAt { get; set; }
}

public static class OutboxMessageTypes
{
    public const string DomainEvent = "DomainEvent";
    public const string ScheduledTrigger = "ScheduledTrigger";
}

public static class QueueTypes
{
    public const string Default = "default";
    public const string LongRunning = "longrunning";
}
