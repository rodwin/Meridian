using Application.Abstractions.Messaging;
using Application.Jobs.Get;
using Application.Jobs.Schedules.Get;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Jobs.Schedules;

internal sealed class GetSchedules : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("jobs/{jobId:guid}/schedules", async (
            Guid jobId,
            IQueryHandler<GetJobSchedulesQuery, List<JobScheduleResponse>> handler,
            CancellationToken cancellationToken) =>
        {
            Result<List<JobScheduleResponse>> result = await handler.Handle(new GetJobSchedulesQuery(jobId), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Jobs)
        .RequireAuthorization();
    }
}
