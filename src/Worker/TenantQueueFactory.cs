using Azure.Messaging.ServiceBus.Administration;

namespace Worker;

internal static class TenantQueueFactory
{
    // The dedup window must exceed the outbox polling interval (10 s) plus any
    // realistic retry delay. If a worker crashes between Phase 2 (ASB enqueue)
    // and Phase 3 (mark IsRelayed), the reaper resets the claim and the next
    // poll sends the same message again with the same MessageId. ASB silently
    // drops it if the original arrived within this window.
    // 2 minutes gives a comfortable margin above the 10-second poll cycle.
    private static readonly TimeSpan DuplicateDetectionWindow = TimeSpan.FromMinutes(2);

    internal static CreateQueueOptions BuildOptions(string queueName) => new(queueName)
    {
        MaxDeliveryCount = 5,
        LockDuration = TimeSpan.FromMinutes(2),
        DefaultMessageTimeToLive = TimeSpan.FromDays(7),
        RequiresDuplicateDetection = true,
        DuplicateDetectionHistoryTimeWindow = DuplicateDetectionWindow
    };
}
