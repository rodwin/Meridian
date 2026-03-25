using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Jobs.Steps.Reorder;

internal sealed class ReorderJobStepsCommandHandler(
    IApplicationDbContext context) : ICommandHandler<ReorderJobStepsCommand>
{
    public async Task<Result> Handle(ReorderJobStepsCommand command, CancellationToken cancellationToken)
    {
        Job? job = await context.Jobs
            .Include(j => j.Steps)
            .FirstOrDefaultAsync(j => j.Id == command.JobId, cancellationToken);

        if (job is null)
        {
            return Result.Failure(JobErrors.NotFound(command.JobId));
        }

        Result result = job.ReorderSteps(command.StepIds);

        if (result.IsFailure)
        {
            return result;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
