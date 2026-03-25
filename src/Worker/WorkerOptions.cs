namespace Worker;

public sealed class WorkerOptions
{
    public bool UseLocalJobQueue { get; set; }

    public List<QueueTypeConfig> Queues { get; set; } = [];

    // Caps the number of tenant outboxes processed concurrently per poll cycle.
    // ProcessTenantOutboxAsync is IO-bound (DB + ASB), so this should be set well
    // above the worker's CPU count. Tune down if DB connection pool pressure appears.
    public int MaxConcurrentTenants { get; set; } = 10;
}

public sealed class QueueTypeConfig
{
    public string Type { get; set; } = string.Empty;
    public int MaxConcurrentCalls { get; set; } = 2;
}
