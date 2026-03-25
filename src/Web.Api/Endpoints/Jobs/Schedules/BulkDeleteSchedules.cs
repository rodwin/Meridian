using Application.Abstractions.Messaging;
using Application.Jobs.Schedules.BulkDelete;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Jobs.Schedules;

internal sealed class BulkDeleteSchedules : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("jobs/{jobId:guid}/schedules/bulk", async (
            Guid jobId,
            [FromBody] List<BulkDeleteScheduleItem> schedules,
            ICommandHandler<BulkDeleteJobSchedulesCommand, BulkOperationResponse<ScheduleDeleteResult>> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new BulkDeleteJobSchedulesCommand
            {
                JobId = jobId,
                Schedules = schedules
            };

            Result<BulkOperationResponse<ScheduleDeleteResult>> result =
                await handler.Handle(command, cancellationToken);

            return result.Match(
                response => Results.Ok(response),
                CustomResults.Problem);
        })
        .WithTags(Tags.Jobs)
        .RequireAuthorization();
    }
}
