using Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database;

public sealed class RoutingDbContext(DbContextOptions<RoutingDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(t => t.TenantId);
            entity.Property(t => t.TenantId).HasMaxLength(100);
            entity.Property(t => t.ConnectionString).HasMaxLength(500).IsRequired();
            entity.Property(t => t.IsActive).HasDefaultValue(true);
            entity.Property(t => t.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });
    }
}
