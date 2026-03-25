using Infrastructure.Database;
using Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Web.Api.Extensions;

public static class MigrationExtensions
{
    private const string RoutingDbKey = "routing-db";

    public static void ApplyMigrations(this IApplicationBuilder app)
    {
        IServiceProvider services = app.ApplicationServices;

        MigrateRoutingDatabase(services);
        SeedRoutingDatabase(services);
        MigrateTenantDatabases(services);
    }

    private static void MigrateRoutingDatabase(IServiceProvider services)
    {
        using IServiceScope scope = services.CreateScope();
        using RoutingDbContext routingDb = scope.ServiceProvider.GetRequiredService<RoutingDbContext>();
        routingDb.Database.EnsureCreated();
    }

    private static void SeedRoutingDatabase(IServiceProvider services)
    {
        using IServiceScope scope = services.CreateScope();
        using RoutingDbContext routingDb = scope.ServiceProvider.GetRequiredService<RoutingDbContext>();

        IConfiguration configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        List<(string TenantId, string ConnectionString)> tenants = configuration
            .GetSection("ConnectionStrings")
            .GetChildren()
            .Where(cs => !cs.Key.Equals(RoutingDbKey, StringComparison.OrdinalIgnoreCase))
            .Where(cs => !string.IsNullOrEmpty(cs.Value))
            .Select(cs => (cs.Key, cs.Value!))
            .ToList();

        foreach ((string tenantId, string connectionString) in tenants)
        {
            Tenant? existing = routingDb.Tenants.Find(tenantId);

            if (existing is null)
            {
                routingDb.Tenants.Add(new Tenant
                {
                    TenantId = tenantId,
                    ConnectionString = connectionString,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.ConnectionString = connectionString;
            }
        }

        routingDb.SaveChanges();
    }

    private static void MigrateTenantDatabases(IServiceProvider services)
    {
        using IServiceScope scope = services.CreateScope();

        ITenantRegistry registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();
        ILoggerFactory loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        ILogger logger = loggerFactory.CreateLogger(nameof(MigrationExtensions));

        IReadOnlyList<TenantInfo> tenants = registry.GetAllAsync().GetAwaiter().GetResult();

        foreach (TenantInfo tenant in tenants)
        {
            logger.LogInformation("Applying migrations for tenant {TenantId}", tenant.TenantId);

            DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(tenant.ConnectionString, sqlOptions =>
                    sqlOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.App))
                .UseLoggerFactory(loggerFactory)
                .Options;

            using ApplicationDbContext tenantDb = new(options);
            tenantDb.Database.Migrate();

            logger.LogInformation("Migrations applied for tenant {TenantId}", tenant.TenantId);
        }
    }
}
