using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Jobs;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Jobs.Steps.BulkUpdate;

internal sealed class BulkUpdateJobStepsCommandHandler(
    IApplicationDbContext context) : ICommandHandler<BulkUpdateJobStepsCommand, BulkOperationResponse<StepResult>>
{
    private static readonly BulkUpdateStepItemValidator ItemValidator = new();

    public async Task<Result<BulkOperationResponse<StepResult>>> Handle(
        BulkUpdateJobStepsCommand command,
        CancellationToken cancellationToken)
    {
        Job? job = await context.Jobs
            .Include(j => j.Steps)
            .FirstOrDefaultAsync(j => j.Id == command.JobId, cancellationToken);

        if (job is null)
        {
            return Result.Failure<BulkOperationResponse<StepResult>>(
                JobErrors.NotFound(command.JobId));
        }

        var succeeded = new List<(Guid Id, string Name)>();
        var failed = new List<BulkItemFailure>();

        for (int i = 0; i < command.Steps.Count; i++)
        {
            BulkUpdateStepItem item = command.Steps[i];

            ValidationResult validation = await ItemValidator.ValidateAsync(item, cancellationToken);
            if (!validation.IsValid)
            {
                failed.Add(new BulkItemFailure(
                    i, item.StepId, item.Name,
                    validation.Errors.Select(e => e.ErrorMessage).ToList()));
                continue;
            }

            Result result = job.UpdateStep(
                item.StepId,
                item.Name,
                item.StepType,
                item.Parameters,
                item.OnFailure,
                item.IsEnabled);

            if (result.IsFailure)
            {
                failed.Add(new BulkItemFailure(
                    i, item.StepId, item.Name,
                    [result.Error.Description]));
                continue;
            }

            JobStep step = job.Steps.First(s => s.Id == item.StepId);
            context.Entry(step).Property(s => s.RowVersion).OriginalValue = item.RowVersion;

            succeeded.Add((item.StepId, item.Name));
        }

        if (succeeded.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        List<StepResult> results = succeeded
            .Select(s =>
            {
                JobStep step = job.Steps.First(x => x.Id == s.Id);
                return new StepResult(s.Id, s.Name, step.RowVersion);
            })
            .ToList();

        return new BulkOperationResponse<StepResult>(results, failed);
    }
}
