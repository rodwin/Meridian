using Application.Abstractions.Messaging;
using Application.Jobs.Schedules.Delete;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Jobs.Schedules;

internal sealed class DeleteSchedule : IEndpoint
{
    public sealed class DeleteScheduleRequest
    {
        public byte[] RowVersion { get; set; } = [];
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("jobs/{jobId:guid}/schedules/{scheduleId:guid}", async (
            Guid jobId,
            Guid scheduleId,
            [FromBody] DeleteScheduleRequest request,
            ICommandHandler<DeleteJobScheduleCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new DeleteJobScheduleCommand
            {
                JobId = jobId,
                ScheduleId = scheduleId,
                RowVersion = request.RowVersion
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
