using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Scheduling;
using Domain.Jobs;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Jobs.Schedules.BulkUpdate;

internal sealed class BulkUpdateJobSchedulesCommandHandler(
    IApplicationDbContext context,
    ICronExpressionValidator cronValidator) : ICommandHandler<BulkUpdateJobSchedulesCommand, BulkOperationResponse<ScheduleResult>>
{
    public async Task<Result<BulkOperationResponse<ScheduleResult>>> Handle(
        BulkUpdateJobSchedulesCommand command,
        CancellationToken cancellationToken)
    {
        Job? job = await context.Jobs
            .Include(j => j.Schedules)
            .FirstOrDefaultAsync(j => j.Id == command.JobId, cancellationToken);

        if (job is null)
        {
            return Result.Failure<BulkOperationResponse<ScheduleResult>>(
                JobErrors.NotFound(command.JobId));
        }

        var itemValidator = new BulkUpdateScheduleItemValidator(cronValidator);
        var succeeded = new List<(Guid Id, string Name)>();
        var failed = new List<BulkItemFailure>();

        for (int i = 0; i < command.Schedules.Count; i++)
        {
            BulkUpdateScheduleItem item = command.Schedules[i];

            ValidationResult validation = await itemValidator.ValidateAsync(item, cancellationToken);
            if (!validation.IsValid)
            {
                failed.Add(new BulkItemFailure(
                    i, item.ScheduleId, item.Name,
                    validation.Errors.Select(e => e.ErrorMessage).ToList()));
                continue;
            }

            Result result = job.UpdateSchedule(
                item.ScheduleId,
                item.Name,
                item.CronExpression,
                item.TimeZoneId,
                item.IsEnabled);

            if (result.IsFailure)
            {
                failed.Add(new BulkItemFailure(
                    i, item.ScheduleId, item.Name,
                    [result.Error.Description]));
                continue;
            }

            JobSchedule schedule = job.Schedules.First(s => s.Id == item.ScheduleId);
            context.Entry(schedule).Property(s => s.RowVersion).OriginalValue = item.RowVersion;

            succeeded.Add((item.ScheduleId, item.Name));
        }

        if (succeeded.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        List<ScheduleResult> results = succeeded
            .Select(s =>
            {
                JobSchedule schedule = job.Schedules.First(x => x.Id == s.Id);
                return new ScheduleResult(s.Id, s.Name, schedule.RowVersion);
            })
            .ToList();

        return new BulkOperationResponse<ScheduleResult>(results, failed);
    }
}
