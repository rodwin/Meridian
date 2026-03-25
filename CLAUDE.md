# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test Commands

```bash
# Build
dotnet build Meridian.sln

# Run all tests
dotnet test Meridian.sln

# Run a specific test project
dotnet test tests/Application.IntegrationTests/Application.IntegrationTests.csproj

# Run a single test by name
dotnet test tests/Application.IntegrationTests/Application.IntegrationTests.csproj --filter "BulkAddSchedules_ShouldPersistAllValidSchedules"

# Run Web.Api
dotnet run --project src/Web.Api

# Run Worker
dotnet run --project src/Worker

# Add EF Core migration (requires --context since there are multiple DbContexts)
dotnet ef migrations add MigrationName --project src/Infrastructure --startup-project src/Web.Api --context ApplicationDbContext
```

Integration tests use Testcontainers (Docker required). Tests auto-discover via `IClassFixture<ApplicationSqlServerFixture>` or `IClassFixture<WorkerSqlServerFixture>`.

## Architecture

Multi-tenant SaaS with Clean Architecture. Each tenant has its own SQL Server database; a shared routing database maps tenant IDs to connection strings.

### Layer Dependencies

```
SharedKernel (Result, Error, Entity, IDomainEvent, IAuditableEntity)
    ↑
Domain (aggregates, domain events, no infrastructure dependencies)
    ↑
Application (CQRS handlers, validators, abstractions only)
    ↑
Infrastructure (EF Core, Auth, Outbox, Tenancy, Quartz config)
    ↑
Web.Api / Worker (two hosts for the same application — no business logic duplication)
```

Architecture tests in `tests/ArchitectureTests/` enforce these layer boundaries via NetArchTest.

### CQRS Pattern

Handlers are registered via **Scrutor assembly scanning** (not MediatR pipeline behaviors). Cross-cutting concerns use the **decorator pattern**:

- `ValidationDecorator` — runs FluentValidation before the handler
- `LoggingDecorator` — logs command/query execution

```
services.Scan(scan => scan.FromAssembliesOf(typeof(DependencyInjection))
    .AddClasses(classes => classes.AssignableTo(typeof(ICommandHandler<,>)), publicOnly: false)
    .AsImplementedInterfaces().WithScopedLifetime());

services.Decorate(typeof(ICommandHandler<,>), typeof(ValidationDecorator.CommandHandler<,>));
```

Handlers are `internal sealed` classes. Validators are `internal sealed` and auto-registered from the Application assembly.

### DDD Aggregate Pattern

Mutations to child entities go through the aggregate root. Example: `Job` owns `JobSchedule` and `JobStep` — all modifications via `job.AddSchedule()`, `job.UpdateStep()`, etc. Domain events are raised via `Raise()` from the `Entity` base class.

Factory methods on aggregates (e.g., `Job.Create()`) enforce invariants and generate IDs using `Guid.CreateVersion7()`.

### Outbox Pattern

`ApplicationDbContext.SaveChangesAsync()` automatically converts domain events to `OutboxMessage` records in the same transaction. The Worker's `OutboxProcessorService` relays them to the job queue using a two-phase commit (claim → publish → mark relayed).

### Multi-Tenancy

- Web.Api: `X-Tenant-Id` header → `TenantResolutionMiddleware` → scoped `TenantContext`
- `ApplicationDbContext` reads connection string from `ITenantContext` at scope creation
- Worker iterates all tenants from the routing database

### Web.Api Endpoints

Minimal API endpoints implement `IEndpoint` and are auto-discovered:

```csharp
internal sealed class CreateJob : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("jobs", async (...) => { ... })
            .WithTags(Tags.Jobs)
            .RequireAuthorization();
    }
}
```

### Bulk Endpoints (Partial-Success Pattern)

Bulk management endpoints use a partial-success pattern: valid items are processed, invalid items are reported back with errors. The shared response type lives in `SharedKernel/BulkOperationResponse.cs`:

```csharp
public sealed record BulkOperationResponse<TSuccess>(
    IReadOnlyList<TSuccess> Succeeded,
    IReadOnlyList<BulkItemFailure> Failed);

public sealed record BulkItemFailure(
    int Index, Guid? Id, string? Name, IReadOnlyList<string> Errors);
```

**Two validation layers per bulk handler:**

1. **Top-level validator** (via `ValidationDecorator`) — fails the entire request if `JobId` is empty or the list is empty
2. **Per-item validator** (inside the handler loop) — validates each item individually, adds failures to the `Failed` list without stopping others

**Handler structure** (all bulk handlers follow this pattern):

```csharp
internal sealed class BulkAddJobSchedulesCommandHandler(
    IApplicationDbContext context) : ICommandHandler<BulkAddJobSchedulesCommand, BulkOperationResponse<ScheduleResult>>
{
    private static readonly BulkAddScheduleItemValidator ItemValidator = new();

    public async Task<Result<BulkOperationResponse<ScheduleResult>>> Handle(...)
    {
        // 1. Load aggregate with children
        // 2. Loop items: validate → call domain method → collect succeeded/failed
        // 3. SaveChangesAsync only if any succeeded
        // 4. Return BulkOperationResponse
    }
}
```

**File organization** — each bulk operation gets its own folder:

```
Application/Jobs/Schedules/BulkAdd/
    BulkAddJobSchedulesCommand.cs       — command + DTOs + result record
    BulkAddJobSchedulesCommandValidator.cs  — top-level (JobId, list not empty)
    BulkAddScheduleItemValidator.cs     — per-item (field-level validation)
    BulkAddJobSchedulesCommandHandler.cs
```

**Routing convention** — bulk endpoints use `/bulk` suffix to avoid conflict with single-item endpoints:

```
POST   /api/jobs/{id}/schedules/bulk     — bulk add
PUT    /api/jobs/{id}/schedules/bulk     — bulk update
DELETE /api/jobs/{id}/schedules/bulk     — bulk delete
PUT    /api/jobs/{id}/steps/reorder      — reorder (accepts ordered list of IDs)
```

**Key difference from creation:** `Job.Create()` is all-or-nothing (validated by `ValidationDecorator` with `RuleForEach`). Bulk management endpoints are partial-success (validated per-item inside the handler).

### Worker Service

Same Application + Infrastructure layers as Web.Api, different host. Runs Quartz.NET (in-memory scheduler), outbox relay, and Azure Service Bus consumers. Toggle `Worker:UseLocalJobQueue = true` for local dev without Azure.

### Optimistic Concurrency (RowVersion)

All domain entities use SQL Server `rowversion` for optimistic concurrency. The `Entity` base class has a `byte[] RowVersion` property; child entities that don't extend `Entity` (e.g., `JobSchedule`, `JobStep`) declare it directly. A convention in `ApplicationDbContext.OnModelCreating` auto-configures any entity with a `RowVersion` property as `.IsRowVersion()` — no per-entity configuration needed.

**For new entities:** Extend `Entity` (gets `RowVersion` automatically) or add `public byte[] RowVersion { get; set; } = [];` for non-aggregate child entities.

**Response DTOs** must include `byte[] RowVersion` so clients receive the current version. `System.Text.Json` serializes `byte[]` as base64 automatically.

**Update/delete commands** must accept `byte[] RowVersion` from the client. Handlers set the original value before saving:

```csharp
context.Entry(entity).Property(e => e.RowVersion).OriginalValue = command.RowVersion;
await context.SaveChangesAsync(cancellationToken);
```

**Bulk update/delete items** each carry their own `RowVersion`. Set `OriginalValue` per item inside the loop.

**Bulk add result records** include `RowVersion` — build results after `SaveChangesAsync` so the DB-generated value is available.

**Exception handling:** `GlobalExceptionHandler` catches `DbUpdateConcurrencyException` and returns `409 Conflict` with a ProblemDetails response. Handlers do **not** catch this exception themselves.

**Validators** require `RowVersion` via `RuleFor(c => c.RowVersion).NotEmpty()`.

## Code Conventions

- **TreatWarningsAsErrors = true** with SonarAnalyzer.CSharp — code must compile warning-free
- **File-scoped namespaces** enforced as error (including EF migrations generated with block-scoped — must be converted)
- **Explicit typing** — use `var` only when the type is apparent from the right side; never for built-in types
- **`is null` / `is not null`** — not `== null` / `!= null`
- **`internal sealed`** by default for handlers, validators, services, configurations
- **Primary constructors** for dependency injection
- **`Guid.CreateVersion7()`** for all entity IDs (time-ordered, client-generated)
- **`ValueGeneratedNever()`** on all EF entity ID configurations (client generates IDs, not the database)
- **Braces required** on all control flow (enforced as error)

## Database

- **Two DbContexts**: `ApplicationDbContext` (per-tenant) and `RoutingDbContext` (shared routing DB)
- **Schemas**: `Schemas.App` for domain entities, `Schemas.Scheduling` for job/step/schedule tables, `Schemas.Outbox` for outbox
- **Migrations** in `src/Infrastructure/Migrations/` — must specify `--context ApplicationDbContext` when generating
- Test projects set `AnalysisMode=None` and `TreatWarningsAsErrors=false` to relax analysis rules
