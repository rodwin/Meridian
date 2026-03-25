using Application.Abstractions.Tenancy;

namespace Infrastructure.Tenancy;

public sealed class TenantContext : ITenantContext
{
    public string TenantId { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
}
