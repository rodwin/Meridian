using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Jobs.Steps.BulkDelete;

internal sealed class BulkDeleteJobStepsCommandHandler(
    IApplicationDbContext context) : ICommandHandler<BulkDeleteJobStepsCommand, BulkOperationResponse<StepDeleteResult>>
{
    public async Task<Result<BulkOperationResponse<StepDeleteResult>>> Handle(
        BulkDeleteJobStepsCommand command,
        CancellationToken cancellationToken)
    {
        Job? job = await context.Jobs
            .Include(j => j.Steps)
            .FirstOrDefaultAsync(j => j.Id == command.JobId, cancellationToken);

        if (job is null)
        {
            return Result.Failure<BulkOperationResponse<StepDeleteResult>>(
                JobErrors.NotFound(command.JobId));
        }

        var succeeded = new List<StepDeleteResult>();
        var failed = new List<BulkItemFailure>();

        for (int i = 0; i < command.Steps.Count; i++)
        {
            BulkDeleteStepItem item = command.Steps[i];

            JobStep? step = job.Steps.FirstOrDefault(s => s.Id == item.StepId);
            if (step is not null)
            {
                context.Entry(step).Property(s => s.RowVersion).OriginalValue = item.RowVersion;
            }

            Result result = job.RemoveStep(item.StepId);

            if (result.IsFailure)
            {
                failed.Add(new BulkItemFailure(
                    i, item.StepId, null,
                    [result.Error.Description]));
                continue;
            }

            succeeded.Add(new StepDeleteResult(item.StepId));
        }

        if (succeeded.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return new BulkOperationResponse<StepDeleteResult>(succeeded, failed);
    }
}
