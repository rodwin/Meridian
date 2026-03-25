using SharedKernel;

namespace Domain.Jobs;

public sealed class JobSchedule : IAuditableEntity
{
    public Guid Id { get; private set; }

    public Guid JobId { get; private set; }

    public string Name { get; internal set; } = string.Empty;

    public string CronExpression { get; internal set; } = string.Empty;

    // IANA or Windows timezone ID (e.g. "UTC", "New Zealand Standard Time").
    public string TimeZoneId { get; internal set; } = string.Empty;

    public bool IsEnabled { get; internal set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = [];

    internal static JobSchedule Create(Guid jobId, string name, string cronExpression, string timeZoneId)
    {
        return new JobSchedule
        {
            Id = Guid.CreateVersion7(),
            JobId = jobId,
            Name = name,
            CronExpression = cronExpression,
            TimeZoneId = timeZoneId,
            IsEnabled = true
        };
    }
}
