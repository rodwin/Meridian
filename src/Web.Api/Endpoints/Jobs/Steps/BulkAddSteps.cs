using Application.Abstractions.Messaging;
using Application.Jobs.Steps.BulkAdd;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Jobs.Steps;

internal sealed class BulkAddSteps : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("jobs/{jobId:guid}/steps/bulk", async (
            Guid jobId,
            List<BulkAddStepItem> steps,
            ICommandHandler<BulkAddJobStepsCommand, BulkOperationResponse<StepResult>> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new BulkAddJobStepsCommand
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
