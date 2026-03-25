using Application.Abstractions.Messaging;
using Application.Jobs.Schedules.BulkAdd;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Jobs.Schedules;

internal sealed class BulkAddSchedules : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("jobs/{jobId:guid}/schedules/bulk", async (
            Guid jobId,
            List<BulkAddScheduleItem> schedules,
            ICommandHandler<BulkAddJobSchedulesCommand, BulkOperationResponse<ScheduleResult>> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new BulkAddJobSchedulesCommand
            {
                JobId = jobId,
                Schedules = schedules
            };

            Result<BulkOperationResponse<ScheduleResult>> result =
                await handler.Handle(command, cancellationToken);

            return result.Match(
                response => Results.Ok(response),
                CustomResults.Problem);
        })
        .WithTags(Tags.Jobs)
        .RequireAuthorization();
    }
}
