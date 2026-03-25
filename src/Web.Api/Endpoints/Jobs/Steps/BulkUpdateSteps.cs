using Application.Abstractions.Messaging;
using Application.Jobs.Steps.BulkUpdate;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Jobs.Steps;

internal sealed class BulkUpdateSteps : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("jobs/{jobId:guid}/steps/bulk", async (
            Guid jobId,
            List<BulkUpdateStepItem> steps,
            ICommandHandler<BulkUpdateJobStepsCommand, BulkOperationResponse<StepResult>> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new BulkUpdateJobStepsCommand
            {
                JobId = jobId,
                Steps = steps
            };

            Result<BulkOperationResponse<StepResult>> result =
                await handler.Handle(command, cancellationToken);

            return result.Match(
                response => Results.Ok(response),
                CustomResults.Problem);
        })
        .WithTags(Tags.Jobs)
        .RequireAuthorization();
    }
}
