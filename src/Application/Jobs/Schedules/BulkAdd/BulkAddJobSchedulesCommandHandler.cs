using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Scheduling;
using Domain.Jobs;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Jobs.Schedules.BulkAdd;

internal sealed class BulkAddJobSchedulesCommandHandler(
    IApplicationDbContext context,
    ICronExpressionValidator cronValidator) : ICommandHandler<BulkAddJobSchedulesCommand, BulkOperationResponse<ScheduleResult>>
{
    public async Task<Result<BulkOperationResponse<ScheduleResult>>> Handle(
        BulkAddJobSchedulesCommand command,
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

        var itemValidator = new BulkAddScheduleItemValidator(cronValidator);
        var succeeded = new List<(Guid Id, string Name)>();
        var failed = new List<BulkItemFailure>();

        for (int i = 0; i < command.Schedules.Count; i++)
        {
            BulkAddScheduleItem item = command.Schedules[i];

            ValidationResult validation = await itemValidator.ValidateAsync(item, cancellationToken);
            if (!validation.IsValid)
            {
                failed.Add(new BulkItemFailure(
                    i, null, item.Name,
                    validation.Errors.Select(e => e.ErrorMessage).ToList()));
                continue;
            }

            Result<Guid> result = job.AddSchedule(item.Name, item.CronExpression, item.TimeZoneId);

            if (result.IsFailure)
            {
                failed.Add(new BulkItemFailure(
                    i, null, item.Name,
                    [result.Error.Description]));
                continue;
            }

            succeeded.Add((result.Value, item.Name));
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
