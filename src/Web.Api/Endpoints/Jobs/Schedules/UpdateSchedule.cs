using Application.Abstractions.Messaging;
using Application.Jobs.Schedules.Update;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Jobs.Schedules;

internal sealed class UpdateSchedule : IEndpoint
{
    public sealed class UpdateScheduleRequest
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

        public bool IsEnabled { get; set; }

        public byte[] RowVersion { get; set; } = [];
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("jobs/{jobId:guid}/schedules/{scheduleId:guid}", async (
            Guid jobId,
            Guid scheduleId,
            UpdateScheduleRequest request,
            ICommandHandler<UpdateJobScheduleCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateJobScheduleCommand
            {
                JobId = jobId,
                ScheduleId = scheduleId,
                Name = request.Name,
                CronExpression = request.CronExpression,
                TimeZoneId = request.TimeZoneId,
                IsEnabled = request.IsEnabled,
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
