using System.Diagnostics;
using Application.Abstractions.Data;
using Domain.Jobs;
using Domain.Todos;
using Domain.Users;
using Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SharedKernel;

namespace Infrastructure.Database;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        TypeNameHandling = TypeNameHandling.All
    };

    public DbSet<User> Users { get; set; }

    public DbSet<TodoItem> TodoItems { get; set; }

    public DbSet<OutboxMessage> OutboxMessages { get; set; }

    public DbSet<Job> Jobs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        modelBuilder.HasDefaultSchema(Schemas.App);

        // Convention: all IAuditableEntity types get consistent audit column configuration.
        // Add new entity types — just implement IAuditableEntity; no config needed here.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
            .Where(t => typeof(IAuditableEntity).IsAssignableFrom(t.ClrType)))
        {
            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(IAuditableEntity.CreatedAt))
                .IsRequired();

            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(IAuditableEntity.UpdatedAt))
                .IsRequired();
        }

        // Convention: any entity with a RowVersion property gets SQL Server row versioning.
        // Add new entity types — just include a byte[] RowVersion property; no config needed.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
            .Where(t => t.FindProperty("RowVersion") is not null))
        {
            modelBuilder.Entity(entityType.ClrType)
                .Property("RowVersion")
                .IsRowVersion();
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AddOutboxMessages();

        return await base.SaveChangesAsync(cancellationToken);
    }

    private void AddOutboxMessages()
    {
        var outboxMessages = ChangeTracker
            .Entries<Entity>()
            .Select(e => e.Entity)
            .SelectMany(entity =>
            {
                List<IDomainEvent> events = entity.DomainEvents;
                entity.ClearDomainEvents();
                return events;
            })
            .Select(domainEvent => new OutboxMessage
            {
                Id = Guid.CreateVersion7(),
                MessageType = OutboxMessageTypes.DomainEvent,
                QueueType = QueueTypes.Default,
                TraceParent = Activity.Current?.Id,
                Type = domainEvent.GetType().Name,
                Payload = JsonConvert.SerializeObject(domainEvent, SerializerSettings),
                OccurredOnUtc = DateTime.UtcNow
            })
            .ToList();

        OutboxMessages.AddRange(outboxMessages);
    }
}
