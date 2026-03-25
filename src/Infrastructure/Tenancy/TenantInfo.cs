namespace Infrastructure.Tenancy;

public sealed class TenantInfo
{
    public TenantInfo() { }

    public TenantInfo(string tenantId, string connectionString)
    {
        TenantId = tenantId;
        ConnectionString = connectionString;
    }

    public string TenantId { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
}
