using Application.Jobs.Get;

namespace Application.IntegrationTests.Jobs;

public sealed class GetJobQueryHandlerTests(ApplicationSqlServerFixture fixture)
    : IClassFixture<ApplicationSqlServerFixture>
{
    [Fact]
    public async Task GetJob_WhenJobExists_ShouldReturnJobWithSchedulesAndSteps()
    {
        // Arrange
        Job job = await SeedJobAsync();
        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new GetJobQueryHandler(context);

        // Act
        Result<JobResponse> result = await handler.Handle(new GetJobQuery(job.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        JobResponse response = result.Value;
        response.Id.ShouldBe(job.Id);
        response.Name.ShouldBe(job.Name);
        response.IsEnabled.ShouldBeTrue();

        response.Steps.ShouldNotBeEmpty();
        JobStepResponse step = response.Steps[0];
        step.Name.ShouldBe("Step 1");
        step.StepType.ShouldBe("RunDatabaseLoad");
        step.RowVersion.ShouldNotBeEmpty();

        response.Schedules.ShouldNotBeEmpty();
        JobScheduleResponse schedule = response.Schedules[0];
        schedule.Name.ShouldBe("Daily");
        schedule.CronExpression.ShouldBe("0 0 10 * * ?");
        schedule.TimeZoneId.ShouldBe("UTC");
        schedule.IsEnabled.ShouldBeTrue();
        schedule.RowVersion.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GetJob_WhenJobHasMultipleSteps_ShouldReturnStepsOrderedByStepOrder()
    {
        // Arrange
        Job job = await SeedJobWithMultipleStepsAsync();
        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new GetJobQueryHandler(context);

        // Act
        Result<JobResponse> result = await handler.Handle(new GetJobQuery(job.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Steps.Count.ShouldBe(2);
        result.Value.Steps[0].StepOrder.ShouldBeLessThan(result.Value.Steps[1].StepOrder);
    }

    [Fact]
    public async Task GetJob_WhenJobNotFound_ShouldReturnFailure()
    {
        // Arrange
        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new GetJobQueryHandler(context);

        // Act
        Result<JobResponse> result = await handler.Handle(
            new GetJobQuery(Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Job.NotFound");
    }

    [Fact]
    public async Task GetJob_ShouldMapOnFailureActionToString()
    {
        // Arrange
        Job job = await SeedJobWithMultipleStepsAsync();
        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new GetJobQueryHandler(context);

        // Act
        Result<JobResponse> result = await handler.Handle(new GetJobQuery(job.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Steps[0].OnFailure.ShouldBe("Stop");
        result.Value.Steps[1].OnFailure.ShouldBe("Continue");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Job> SeedJobAsync()
    {
        await using ApplicationDbContext context = fixture.CreateContext();

        Job job = Job.Create(
            $"Test Job {Guid.NewGuid():N}",
            "A test job",
            [("Step 1", "RunDatabaseLoad", null, OnFailureAction.Stop)],
            [("Daily", "0 0 10 * * ?", "UTC")]).Value;

        context.Jobs.Add(job);
        await context.SaveChangesAsync();
        return job;
    }

    private async Task<Job> SeedJobWithMultipleStepsAsync()
    {
        await using ApplicationDbContext context = fixture.CreateContext();

        Job job = Job.Create(
            $"Test Job {Guid.NewGuid():N}",
            null,
            [
                ("Step 1", "RunDatabaseLoad", null, OnFailureAction.Stop),
                ("Step 2", "SendEmail", "{\"to\":\"admin@example.com\"}", OnFailureAction.Continue)
            ],
            [("Daily", "0 0 10 * * ?", "UTC")]).Value;

        context.Jobs.Add(job);
        await context.SaveChangesAsync();
        return job;
    }
}
