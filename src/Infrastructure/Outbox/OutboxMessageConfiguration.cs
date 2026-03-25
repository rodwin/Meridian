using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Outbox;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages", Schemas.Outbox);

        builder.HasKey(m => m.Id);

        builder.Property(m => m.MessageType).HasMaxLength(50).IsRequired();

        builder.Property(m => m.QueueType).HasMaxLength(20).IsRequired().HasDefaultValue(QueueTypes.Default);

        builder.Property(m => m.Type).HasMaxLength(500).IsRequired();

        builder.Property(m => m.Payload).IsRequired();

        builder.Property(m => m.IsRelayed).IsRequired().HasDefaultValue(false);

        builder.Property(m => m.ProcessingStartedAt);

        // Composite index used by the relay poll query:
        // WHERE IsRelayed = 0 AND ProcessingStartedAt IS NULL ORDER BY OccurredOnUtc
        builder.HasIndex(m => new { m.IsRelayed, m.ProcessingStartedAt });
    }
}
