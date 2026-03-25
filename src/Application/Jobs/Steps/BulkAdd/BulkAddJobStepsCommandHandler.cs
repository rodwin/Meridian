using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Jobs;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Jobs.Steps.BulkAdd;

internal sealed class BulkAddJobStepsCommandHandler(
    IApplicationDbContext context) : ICommandHandler<BulkAddJobStepsCommand, BulkOperationResponse<StepResult>>
{
    private static readonly BulkAddStepItemValidator ItemValidator = new();

    public async Task<Result<BulkOperationResponse<StepResult>>> Handle(
        BulkAddJobStepsCommand command,
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
            BulkAddStepItem item = command.Steps[i];

            ValidationResult validation = await ItemValidator.ValidateAsync(item, cancellationToken);
            if (!validation.IsValid)
            {
                failed.Add(new BulkItemFailure(
                    i, null, item.Name,
                    validation.Errors.Select(e => e.ErrorMessage).ToList()));
                continue;
            }

            Result<Guid> result = job.AddStep(item.Name, item.StepType, item.Parameters, item.OnFailure);

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
