using Application.Abstractions.Messaging;
using Application.Jobs.Schedules.Add;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Jobs.Schedules;

internal sealed class AddSchedule : IEndpoint
{
    public sealed class AddScheduleRequest
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Quartz cron expression (6-7 fields, e.g. "0 0 10 * * ?").
        /// </summary>
        public string CronExpression { get; set; } = string.Empty;

        /// <summary>
        /// IANA or Windows timezone ID (e.g. "UTC", "New Zealand Standard Time").
        /// </summary>
        public string TimeZoneId { get; set; } = string.Empty;
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("jobs/{jobId:guid}/schedules", async (
            Guid jobId,
            AddScheduleRequest request,
            ICommandHandler<AddJobScheduleCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new AddJobScheduleCommand
            {
                JobId = jobId,
                Name = request.Name,
                CronExpression = request.CronExpression,
                TimeZoneId = request.TimeZoneId
            };

            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(
                id => Results.Created($"/api/v1/jobs/{jobId}/schedules/{id}", new { Id = id }),
                CustomResults.Problem);
        })
        .WithTags(Tags.Jobs)
        .RequireAuthorization();
    }
}
