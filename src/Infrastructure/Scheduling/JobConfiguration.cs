using Domain.Jobs;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Scheduling;

internal sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("Jobs", Schemas.Scheduling);

        builder.HasKey(j => j.Id);

        builder.Property(j => j.Id).ValueGeneratedNever();

        builder.Property(j => j.Name).HasMaxLength(200).IsRequired();

        builder.Property(j => j.Description).HasMaxLength(1000);

        builder.Property(j => j.IsEnabled).IsRequired();

        builder.Property(j => j.CreatedAt).IsRequired();

        builder.Property(j => j.UpdatedAt).IsRequired();

        builder.HasMany(j => j.Steps)
            .WithOne()
            .HasForeignKey(s => s.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(j => j.Steps).HasField("_steps");

        builder.HasMany(j => j.Schedules)
            .WithOne()
            .HasForeignKey(s => s.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(j => j.Schedules).HasField("_schedules");
    }
}
