namespace Web.Api.Infrastructure;

public sealed class TenantEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // TenantResolutionMiddleware runs earlier in the pipeline and populates TenantContext
        // if the header is present. This filter's only job is to enforce that tenant-scoped
        // endpoints are never reached without it — returning a clean 400 instead of letting
        // the request fail later with a cryptic DB or null-reference error.
        if (!context.HttpContext.Request.Headers.ContainsKey("X-Tenant-Id"))
        {
            return Results.BadRequest("X-Tenant-Id header is required.");
        }

        return await next(context);
    }
}
