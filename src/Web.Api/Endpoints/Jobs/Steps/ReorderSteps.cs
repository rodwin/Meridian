using Application.Abstractions.Messaging;
using Application.Jobs.Steps.Reorder;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Jobs.Steps;

internal sealed class ReorderSteps : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("jobs/{jobId:guid}/steps/reorder", async (
            Guid jobId,
            List<Guid> stepIds,
            ICommandHandler<ReorderJobStepsCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new ReorderJobStepsCommand
            {
                JobId = jobId,
                StepIds = stepIds
            };

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(
                () => Results.NoContent(),
                CustomResults.Problem);
        })
        .WithTags(Tags.Jobs)
        .RequireAuthorization();
    }
}
