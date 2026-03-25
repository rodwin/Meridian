using Domain.Jobs;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Scheduling;

internal sealed class JobScheduleConfiguration : IEntityTypeConfiguration<JobSchedule>
{
    public void Configure(EntityTypeBuilder<JobSchedule> builder)
    {
        builder.ToTable("JobSchedules", Schemas.Scheduling);

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();

        builder.Property(s => s.CronExpression).HasMaxLength(100).IsRequired();

        builder.Property(s => s.TimeZoneId).HasMaxLength(100).IsRequired();

        builder.Property(s => s.IsEnabled).IsRequired();

        // Primary lookup: all enabled schedules for a job (used by Worker on startup).
        builder.HasIndex(s => new { s.JobId, s.IsEnabled });
    }
}
