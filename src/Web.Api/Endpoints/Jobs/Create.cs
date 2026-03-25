using Application.Abstractions.Messaging;
using Application.Jobs.Create;
using Domain.Jobs;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Jobs;

internal sealed class Create : IEndpoint
{
    public sealed class CreateJobRequest
    {
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        /// <summary>
        /// Ordered steps executed sequentially when the job fires.
        /// StepOrder is derived from the array position (1-based).
        /// </summary>
        public List<CreateJobStepItem> Steps { get; set; } = [];

        /// <summary>
        /// One or more cron schedules that independently trigger this job.
        /// </summary>
        public List<CreateJobScheduleItem> Schedules { get; set; } = [];
    }

    public sealed class CreateJobStepItem
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Identifies the command handler that executes this step (e.g. "RunDatabaseLoad").
        /// </summary>
        public string StepType { get; set; } = string.Empty;

        /// <summary>
        /// JSON-serialised configuration passed to the step handler at runtime.
        /// </summary>
        public string? Parameters { get; set; }

        public OnFailureAction OnFailure { get; set; }
    }

    public sealed class CreateJobScheduleItem
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
        app.MapPost("jobs", async (
            CreateJobRequest request,
            ICommandHandler<CreateJobCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateJobCommand
            {
                Name = request.Name,
                Description = request.Description,
                Steps = request.Steps
                    .Select(s => new CreateJobStepRequest
                    {
                        Name = s.Name,
                        StepType = s.StepType,
                        Parameters = s.Parameters,
                        OnFailure = s.OnFailure
                    })
                    .ToList(),
                Schedules = request.Schedules
                    .Select(s => new CreateJobScheduleRequest
                    {
                        Name = s.Name,
                        CronExpression = s.CronExpression,
                        TimeZoneId = s.TimeZoneId
                    })
                    .ToList()
            };

            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(
                id => Results.Created($"/api/v1/jobs/{id}", new { Id = id }),
                CustomResults.Problem);
        })
        .WithTags(Tags.Jobs)
        .RequireAuthorization();
    }
}
