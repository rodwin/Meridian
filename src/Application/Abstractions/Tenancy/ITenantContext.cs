namespace Application.Abstractions.Tenancy;

public interface ITenantContext
{
    string TenantId { get; }
    string ConnectionString { get; }
}
