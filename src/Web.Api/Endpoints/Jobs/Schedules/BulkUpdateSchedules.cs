using Application.Abstractions.Messaging;
using Application.Jobs.Schedules.BulkUpdate;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Jobs.Schedules;

internal sealed class BulkUpdateSchedules : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("jobs/{jobId:guid}/schedules/bulk", async (
            Guid jobId,
            List<BulkUpdateScheduleItem> schedules,
            ICommandHandler<BulkUpdateJobSchedulesCommand, BulkOperationResponse<ScheduleResult>> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new BulkUpdateJobSchedulesCommand
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
