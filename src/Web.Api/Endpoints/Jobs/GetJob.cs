using Application.Abstractions.Messaging;
using Application.Jobs.Get;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Jobs;

internal sealed class GetJob : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("jobs/{id:guid}", async (
            Guid id,
            IQueryHandler<GetJobQuery, JobResponse> handler,
            CancellationToken cancellationToken) =>
        {
            Result<JobResponse> result = await handler.Handle(new GetJobQuery(id), cancellationToken);

            return result.Match(CustomResults.OkEnvelope, CustomResults.Problem);
        })
        .WithTags(Tags.Jobs)
        .RequireAuthorization();
    }
}
