using Domain.Jobs;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Scheduling;

internal sealed class JobStepConfiguration : IEntityTypeConfiguration<JobStep>
{
    public void Configure(EntityTypeBuilder<JobStep> builder)
    {
        builder.ToTable("JobSteps", Schemas.Scheduling);

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();

        builder.Property(s => s.StepType).HasMaxLength(100).IsRequired();

        builder.Property(s => s.Parameters);

        builder.Property(s => s.OnFailure).IsRequired();

        builder.HasIndex(s => new { s.JobId, s.StepOrder });
    }
}
