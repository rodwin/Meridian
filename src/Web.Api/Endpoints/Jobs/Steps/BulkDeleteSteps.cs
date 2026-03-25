using Application.Abstractions.Messaging;
using Application.Jobs.Steps.BulkDelete;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Jobs.Steps;

internal sealed class BulkDeleteSteps : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("jobs/{jobId:guid}/steps/bulk", async (
            Guid jobId,
            [FromBody] List<BulkDeleteStepItem> steps,
            ICommandHandler<BulkDeleteJobStepsCommand, BulkOperationResponse<StepDeleteResult>> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new BulkDeleteJobStepsCommand
            {
                JobId = jobId,
                Steps = steps
            };

            Result<BulkOperationResponse<StepDeleteResult>> result =
                await handler.Handle(command, cancellationToken);

            return result.Match(
                response => Results.Ok(response),
                CustomResults.Problem);
        })
        .WithTags(Tags.Jobs)
        .RequireAuthorization();
    }
}
