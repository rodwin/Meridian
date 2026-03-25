using System.Reflection;
using Application;
using HealthChecks.UI.Client;
using Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Web.Api;
using Web.Api.Extensions;
using Web.Api.Infrastructure;
using Web.Api.Middleware;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOpenApiWithAuth();

builder.Services
    .AddApplication()
    .AddPresentation()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddEndpoints(Assembly.GetExecutingAssembly());

WebApplication app = builder.Build();

app.MapDefaultEndpoints();

// All endpoints require X-Tenant-Id — every handler hits a tenant DB via ApplicationDbContext.
RouteGroupBuilder apiGroup = app.NewVersionedApi()
    .MapGroup("api")
    .HasApiVersion(1)
    .AddEndpointFilter<TenantEndpointFilter>();

app.MapEndpoints(apiGroup);

if (app.Environment.IsDevelopment())
{
    app.UseOpenWithUi();

    app.ApplyMigrations();
}

app.MapHealthChecks("health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.UseRequestContextLogging();

app.UseExceptionHandler();
app.UseRouting();

app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthentication();

app.UseAuthorization();

// REMARK: If you want to use Controllers, you'll need this.
app.MapControllers();

await app.RunAsync();

// REMARK: Required for functional and integration tests to work.
namespace Web.Api
{
    public partial class Program;
}
