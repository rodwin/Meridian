namespace Infrastructure.Tenancy;

public interface ITenantRegistry
{
    Task<string?> GetConnectionStringAsync(string tenantId);
    Task<IReadOnlyList<TenantInfo>> GetAllAsync();
}
