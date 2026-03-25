using Infrastructure.Tenancy;

namespace Web.Api.Middleware;

public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantRegistry registry, TenantContext tenantContext)
    {
        if (!context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantIdValues) ||
            string.IsNullOrWhiteSpace(tenantIdValues))
        {
            await _next(context);
            return;
        }

        string tenantId = tenantIdValues.ToString();

        var connectionString = await registry.GetConnectionStringAsync(tenantId);

        if (connectionString is null)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync($"Tenant '{tenantId}' not found.");
            return;
        }

        tenantContext.TenantId = tenantId;
        tenantContext.ConnectionString = connectionString;

        await _next(context);
    }
}
