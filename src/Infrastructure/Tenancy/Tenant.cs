namespace Infrastructure.Tenancy;

public sealed class Tenant
{
    public string TenantId { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
