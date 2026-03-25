using Application;
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Tenancy;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Domain.Jobs;
using Infrastructure;
using Infrastructure.Authentication;
using Infrastructure.Database;
using Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Quartz;
using SharedKernel;
using Worker;
using Worker.Messaging;
using Worker.Scheduling;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// ── Routing database ─────────────────────────────────────────────────────────
// Shared (non-tenant) database that holds the tenant registry.
// Used to resolve tenant IDs → connection strings.
string? routingDbConnectionString = builder.Configuration.GetConnectionString("routing-db");
builder.Services.AddDbContext<RoutingDbContext>(options =>
    options.UseSqlServer(routingDbConnectionString));

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ITenantRegistry, DatabaseTenantRegistry>();
builder.Services.AddSingleton(TimeProvider.System);

// ── Per-tenant database ──────────────────────────────────────────────────────
// Each tenant has its own database. ApplicationDbContext is scoped so that
// MessageDispatcher can populate TenantContext before resolving it, routing
// every handler to the correct tenant's database without ambient state.
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
{
    string connectionString = sp.GetRequiredService<ITenantContext>().ConnectionString;
    options.UseSqlServer(connectionString, sqlOptions =>
        sqlOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.App));
});
builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

// ── Application & domain layer ───────────────────────────────────────────────
// The Worker reuses the same Application and Infrastructure layers as the Web API —
// no business logic is duplicated. Handlers don't know or care which host triggered them.
builder.Services.AddApplication();
builder.Services.AddDomainEventDispatching();
builder.Services.AddScheduling();

// ICurrentUserService is used by ApplicationDbContext to populate audit fields.
// In the Worker there is no HTTP context, so UserId returns null — audit fields
// will be null for system-initiated operations, which is the correct behaviour.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// ── Worker options ───────────────────────────────────────────────────────────
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));

// MessageDispatcher is shared by both IJobQueue implementations.
// It deserialises the job message and dispatches to the appropriate handler via MediatR.
builder.Services.AddSingleton<MessageDispatcher>();

// ── Job queue ────────────────────────────────────────────────────────────────
// IJobQueue is the outbox relay's publish target. Two implementations exist:
//   InMemoryJobQueue  — local dev, dispatches inline without any Azure dependency.
//   ServiceBusJobQueue — production, publishes to per-tenant ASB queues.
//
// The switch is driven by Worker:UseLocalJobQueue in appsettings so the same
// binary runs locally and in production without code changes.
bool useLocalJobQueue = builder.Configuration.GetValue<bool>("Worker:UseLocalJobQueue");

if (useLocalJobQueue)
{
    builder.Services.AddSingleton<IJobQueue, InMemoryJobQueue>();
}
else
{
    string serviceBusConnectionString = builder.Configuration.GetConnectionString("service-bus")!;

    builder.Services.AddSingleton<ServiceBusClient>(
        _ => new ServiceBusClient(serviceBusConnectionString));

    // Administration client is used by ServiceBusProvisioningService (startup bulk
    // provisioning) and ServiceBusConsumerService (runtime provisioning for new tenants).
    builder.Services.AddSingleton<ServiceBusAdministrationClient>(
        _ => new ServiceBusAdministrationClient(serviceBusConnectionString));

    builder.Services.AddSingleton<IJobQueue, ServiceBusJobQueue>();

    // Provisions ASB queues for all known tenants before the consumer starts.
    // Runs as IHostedService so StartAsync completes before BackgroundService.ExecuteAsync.
    builder.Services.AddHostedService<ServiceBusProvisioningService>();

    // Starts one ServiceBusProcessor per tenant per queue type and reconciles
    // the processor set periodically to handle tenants added or removed at runtime.
    builder.Services.AddHostedService<ServiceBusConsumerService>();
}

// ── Quartz scheduler ─────────────────────────────────────────────────────────
// In-memory store only — tenant databases are the source of truth for schedules.
// The scheduler is rebuilt from each tenant's ScheduledJobs table on every startup.
// Do NOT use a persistent Quartz store unless moving to a multi-pod scheduler setup.
//
// Registration order matters: QuartzHostedService must start before ScheduleLoaderService
// so the scheduler is running when LoadAllAsync tries to register triggers.
builder.Services.AddTransient<TenantScheduledJob>();
builder.Services.AddSingleton<TenantScheduleManager>();
builder.Services.AddQuartz();
builder.Services.AddQuartzHostedService(opt => opt.WaitForJobsToComplete = true);
builder.Services.AddHostedService<ScheduleLoaderService>();

builder.Services.AddScoped<JobScheduleChangedHandler>();
builder.Services.AddScoped<IDomainEventHandler<JobScheduleAddedDomainEvent>>(
    sp => sp.GetRequiredService<JobScheduleChangedHandler>());
builder.Services.AddScoped<IDomainEventHandler<JobScheduleUpdatedDomainEvent>>(
    sp => sp.GetRequiredService<JobScheduleChangedHandler>());
builder.Services.AddScoped<IDomainEventHandler<JobScheduleDeletedDomainEvent>>(
    sp => sp.GetRequiredService<JobScheduleChangedHandler>());

// ── Background services ──────────────────────────────────────────────────────
// Polls each tenant's outbox table and relays pending messages to the job queue.
// Runs regardless of which IJobQueue implementation is registered.
builder.Services.AddHostedService<OutboxProcessorService>();

// ── Startup ──────────────────────────────────────────────────────────────────
IHost host = builder.Build();

// Ensure the routing database schema exists before any hosted service starts.
// EnsureCreatedAsync is safe to call repeatedly — it is a no-op if the schema
// is already present. For tenant databases, migrations run per-tenant separately.
using (IServiceScope scope = host.Services.CreateScope())
{
    RoutingDbContext routingDb = scope.ServiceProvider.GetRequiredService<RoutingDbContext>();
    await routingDb.Database.EnsureCreatedAsync();
}

await host.RunAsync();
