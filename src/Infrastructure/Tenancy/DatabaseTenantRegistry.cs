using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Infrastructure.Database;

namespace Infrastructure.Tenancy;

public sealed class DatabaseTenantRegistry(
    IServiceScopeFactory scopeFactory,
    IMemoryCache cache) : ITenantRegistry
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<string?> GetConnectionStringAsync(string tenantId)
    {
        string cacheKey = $"tenant-conn:{tenantId}";

        if (cache.TryGetValue(cacheKey, out string? cached))
        {
            return cached;
        }

        using IServiceScope scope = scopeFactory.CreateScope();
        RoutingDbContext db = scope.ServiceProvider.GetRequiredService<RoutingDbContext>();

        string? connectionString = await db.Tenants
            .Where(t => t.TenantId == tenantId && t.IsActive)
            .Select(t => t.ConnectionString)
            .FirstOrDefaultAsync();

        cache.Set(cacheKey, connectionString, CacheDuration);

        return connectionString;
    }

    public async Task<IReadOnlyList<TenantInfo>> GetAllAsync()
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        RoutingDbContext db = scope.ServiceProvider.GetRequiredService<RoutingDbContext>();

        return await db.Tenants
            .Where(t => t.IsActive)
            .Select(t => new TenantInfo
            {
                TenantId = t.TenantId,
                ConnectionString = t.ConnectionString
            })
            .ToListAsync();
    }
}
