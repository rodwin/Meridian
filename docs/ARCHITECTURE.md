# DevHabit Architecture

Multi-tenant SaaS built on Clean Architecture. Each tenant has an isolated SQL Server database; a shared routing database maps tenant IDs to connection strings.

---

## Layer Dependencies

```mermaid
graph TD
    SK[SharedKernel<br/><i>Result, Error, Entity,<br/>IDomainEvent, IAuditableEntity</i>]
    D[Domain<br/><i>Aggregates, Domain Events<br/>No infrastructure dependencies</i>]
    A[Application<br/><i>CQRS Handlers, Validators<br/>Abstractions only</i>]
    I[Infrastructure<br/><i>EF Core, Auth, Outbox<br/>Tenancy, Quartz config</i>]
    WEB[Web.Api<br/><i>Minimal API Endpoints<br/>Middleware</i>]
    WRK[Worker<br/><i>Quartz Scheduler<br/>Outbox Relay, ASB Consumers</i>]

    SK --> D --> A --> I --> WEB
    I --> WRK

    style SK fill:#e8f4f8
    style D fill:#d4edda
    style A fill:#fff3cd
    style I fill:#f8d7da
    style WEB fill:#d1ecf1
    style WRK fill:#d1ecf1
```

Web.Api and Worker are two different **hosts** for the same application. All business logic lives in Application and Domain — neither host duplicates it.

---

## System Overview

```mermaid
graph TB
    subgraph Client
        HTTP[HTTP Client]
    end

    subgraph Web.Api
        MW[TenantResolution<br/>Middleware]
        EP[Minimal API<br/>Endpoints]
        VD[ValidationDecorator]
        HD[Command / Query<br/>Handlers]
    end

    subgraph Infrastructure
        ADB[ApplicationDbContext<br/><i>per-tenant</i>]
        RDB[RoutingDbContext<br/><i>shared</i>]
        OB[Outbox<br/>OutboxMessage table]
        TR[TenantContext<br/>+ Registry]
    end

    subgraph Worker
        QZ[Quartz.NET<br/><i>In-memory scheduler</i>]
        TSJ[TenantScheduledJob<br/><i>IJob</i>]
        OPS[OutboxProcessor<br/>Service]
        MD[MessageDispatcher]
        SBC[ServiceBusConsumer<br/>Service]
        WHD[Command / Query<br/>Handlers]
    end

    subgraph Storage
        RD[(Routing DB<br/><i>Tenants table</i>)]
        TD1[(Tenant A DB)]
        TD2[(Tenant B DB)]
        ASB[Azure Service Bus<br/><i>per-tenant queues</i>]
    end

    HTTP -->|X-Tenant-Id header| MW --> EP --> VD --> HD
    HD -->|SaveChangesAsync| ADB
    ADB -->|Domain events → OutboxMessage| OB
    ADB -->|routes to| TD1
    ADB -->|routes to| TD2
    RDB --- RD
    TR -->|lookup| RDB

    QZ -->|fires| TSJ -->|writes| OB
    OPS -->|polls every 10s| OB
    OPS -->|publishes| ASB
    SBC -->|consumes| ASB
    SBC --> MD --> WHD
    WHD -->|SaveChangesAsync| ADB

    style Web.Api fill:#dbeafe,stroke:#3b82f6
    style Worker fill:#dcfce7,stroke:#22c55e
    style Infrastructure fill:#fef9c3,stroke:#eab308
    style Storage fill:#f3e8ff,stroke:#a855f7
    style Client fill:#f1f5f9,stroke:#94a3b8
```

---

## Scheduled Job Execution Flow

```mermaid
sequenceDiagram
    participant Q as Quartz.NET
    participant TSJ as TenantScheduledJob
    participant DB as Tenant DB (Outbox)
    participant OPS as OutboxProcessorService
    participant ASB as Azure Service Bus
    participant SBC as ServiceBusConsumer
    participant MD as MessageDispatcher
    participant H as RunJobCommandHandler

    Q->>TSJ: Fire trigger (cron schedule)
    TSJ->>DB: INSERT OutboxMessage<br/>(MessageType=ScheduledTrigger)

    loop Every 10 seconds
        OPS->>DB: Claim unrelayed messages<br/>(ProcessingStartedAt = NOW)
        OPS->>ASB: Publish to tenant-{id} queue
        OPS->>DB: Mark IsRelayed = true
    end

    ASB->>SBC: Deliver message
    SBC->>MD: DispatchAsync(tenantId, message)
    MD->>MD: Set TenantContext
    MD->>H: Send(RunJobCommand)
    H->>DB: Execute job steps sequentially<br/>SaveChangesAsync → new OutboxMessages
    H-->>SBC: Complete
    SBC->>ASB: CompleteMessageAsync
```

---

## Domain Event Flow (API-Triggered)

```mermaid
sequenceDiagram
    participant C as Client
    participant EP as Endpoint
    participant H as Command Handler
    participant DB as Tenant DB
    participant OPS as OutboxProcessorService
    participant ASB as Azure Service Bus
    participant EH as Domain Event Handler

    C->>EP: POST /api/jobs/{id}/schedules
    EP->>H: AddJobScheduleCommand
    H->>H: job.AddSchedule() → raises<br/>JobScheduleAddedDomainEvent
    H->>DB: SaveChangesAsync()<br/>→ Domain event → OutboxMessage (atomic)
    H-->>EP: Result<ScheduleResult>
    EP-->>C: 201 Created

    OPS->>DB: Poll → Claim → Relay
    OPS->>ASB: Publish
    ASB->>EH: JobScheduleChangedHandler
    EH->>EH: TenantScheduleManager.SyncTenantAsync()<br/>→ Quartz trigger updated in-memory
```

---

## Multi-Tenancy Model

```mermaid
graph LR
    subgraph Routing DB
        T[Tenants table<br/>TenantId → ConnectionString]
    end

    subgraph Per-Tenant DBs
        TA[(Tenant A DB<br/>Users, Jobs, Outbox)]
        TB[(Tenant B DB<br/>Users, Jobs, Outbox)]
        TC[(Tenant C DB<br/>Users, Jobs, Outbox)]
    end

    subgraph Web.Api
        MW2[TenantResolutionMiddleware<br/><i>reads X-Tenant-Id header</i>]
        CTX[TenantContext<br/><i>scoped per request</i>]
    end

    subgraph Worker
        MD2[MessageDispatcher<br/><i>reads tenantId from message</i>]
        CTX2[TenantContext<br/><i>scoped per message</i>]
    end

    subgraph ASB Queues
        QA[tenant-a-default<br/>tenant-a-longrunning]
        QB[tenant-b-default<br/>tenant-b-longrunning]
    end

    MW2 --> CTX --> T
    MD2 --> CTX2 --> T
    T --> TA
    T --> TB
    T --> TC
    QA -.->|consumed by| MD2
    QB -.->|consumed by| MD2
```

---

## Key Patterns

| Pattern | Where Used | Purpose |
|---|---|---|
| **Clean Architecture** | All layers | Isolated business logic, no leaky dependencies |
| **CQRS** | Application layer | Commands mutate state; queries read only |
| **DDD Aggregates** | Domain layer | `Job` owns `JobSchedule` and `JobStep`; mutations via root |
| **Outbox Pattern** | Infrastructure | Guaranteed domain event delivery; crash-safe |
| **Decorator Pattern** | Application layer | `ValidationDecorator`, `LoggingDecorator` wrap handlers |
| **Multi-Tenancy** | Infrastructure | Per-tenant database isolation via `TenantContext` |
| **Optimistic Concurrency** | All entities | SQL Server `rowversion` — 409 Conflict on stale writes |
| **Two-Phase Claim** | OutboxProcessorService | Claim → Relay → Persist; reaper recovers crashed workers |
| **Event-Driven Scheduling** | Worker | `JobScheduleChangedHandler` syncs Quartz without restart |
