using Application.Jobs.Steps.BulkAdd;
using Application.Jobs.Steps.BulkDelete;
using Application.Jobs.Steps.BulkUpdate;
using Application.Jobs.Steps.Reorder;
using AddStepResult = Application.Jobs.Steps.BulkAdd.StepResult;
using UpdateStepResult = Application.Jobs.Steps.BulkUpdate.StepResult;

namespace Application.IntegrationTests.Jobs.Steps;

public sealed class BulkJobStepHandlerTests(ApplicationSqlServerFixture fixture)
    : IClassFixture<ApplicationSqlServerFixture>
{
    // ── BulkAdd ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task BulkAddSteps_ShouldPersistAllValidSteps()
    {
        Job job = await SeedJobAsync();
        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new BulkAddJobStepsCommandHandler(context);

        var command = new BulkAddJobStepsCommand
        {
            JobId = job.Id,
            Steps =
            [
                new BulkAddStepItem
                {
                    Name = "Step A",
                    StepType = "RunDatabaseLoad",
                    Parameters = null,
                    OnFailure = OnFailureAction.Stop
                },
                new BulkAddStepItem
                {
                    Name = "Step B",
                    StepType = "SendEmail",
                    Parameters = """{"to":"admin@test.com"}""",
                    OnFailure = OnFailureAction.Continue
                }
            ]
        };

        Result<BulkOperationResponse<AddStepResult>> result =
            await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Succeeded.Count.ShouldBe(2);
        result.Value.Failed.ShouldBeEmpty();

        await using ApplicationDbContext verify = fixture.CreateContext();
        Job? loaded = await verify.Jobs
            .Include(j => j.Steps)
            .FirstOrDefaultAsync(j => j.Id == job.Id);

        loaded!.Steps.Count.ShouldBe(3); // 1 from seed + 2 added
        loaded.Steps.OrderBy(s => s.StepOrder).First().StepOrder.ShouldBe(1);
        loaded.Steps.OrderBy(s => s.StepOrder).Last().StepOrder.ShouldBe(3);
    }

    [Fact]
    public async Task BulkAddSteps_WithMixOfValidAndInvalid_ShouldPartiallySucceed()
    {
        Job job = await SeedJobAsync();
        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new BulkAddJobStepsCommandHandler(context);

        var command = new BulkAddJobStepsCommand
        {
            JobId = job.Id,
            Steps =
            [
                new BulkAddStepItem
                {
                    Name = "Valid Step",
                    StepType = "RunDatabaseLoad",
                    OnFailure = OnFailureAction.Stop
                },
                new BulkAddStepItem
                {
                    Name = "", // invalid — empty name
                    StepType = "RunDatabaseLoad",
                    OnFailure = OnFailureAction.Stop
                }
            ]
        };

        Result<BulkOperationResponse<AddStepResult>> result =
            await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Succeeded.Count.ShouldBe(1);
        result.Value.Succeeded[0].Name.ShouldBe("Valid Step");
        result.Value.Failed.Count.ShouldBe(1);
        result.Value.Failed[0].Index.ShouldBe(1);
    }

    [Fact]
    public async Task BulkAddSteps_WhenJobNotFound_ShouldReturnFailure()
    {
        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new BulkAddJobStepsCommandHandler(context);

        var command = new BulkAddJobStepsCommand
        {
            JobId = Guid.NewGuid(),
            Steps =
            [
                new BulkAddStepItem
                {
                    Name = "Step",
                    StepType = "RunDatabaseLoad",
                    OnFailure = OnFailureAction.Stop
                }
            ]
        };

        Result<BulkOperationResponse<AddStepResult>> result =
            await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Job.NotFound");
    }

    // ── BulkUpdate ──────────────────────────────────────────────────────────

    [Fact]
    public async Task BulkUpdateSteps_ShouldPersistChanges()
    {
        Job job = await SeedJobAsync();
        (Guid stepId, byte[] rowVersion) = await SeedStepAsync(job.Id, "Original", "RunDatabaseLoad");

        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new BulkUpdateJobStepsCommandHandler(context);

        var command = new BulkUpdateJobStepsCommand
        {
            JobId = job.Id,
            Steps =
            [
                new BulkUpdateStepItem
                {
                    StepId = stepId,
                    Name = "Updated",
                    StepType = "SendEmail",
                    Parameters = """{"to":"admin@test.com"}""",
                    OnFailure = OnFailureAction.Continue,
                    IsEnabled = false,
                    RowVersion = rowVersion
                }
            ]
        };

        Result<BulkOperationResponse<UpdateStepResult>> result =
            await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Succeeded.Count.ShouldBe(1);

        await using ApplicationDbContext verify = fixture.CreateContext();
        Job? loaded = await verify.Jobs
            .Include(j => j.Steps)
            .FirstOrDefaultAsync(j => j.Id == job.Id);

        JobStep step = loaded!.Steps.Single(s => s.Id == stepId);
        step.Name.ShouldBe("Updated");
        step.StepType.ShouldBe("SendEmail");
        step.OnFailure.ShouldBe(OnFailureAction.Continue);
        step.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task BulkUpdateSteps_WithMixOfValidAndNotFound_ShouldPartiallySucceed()
    {
        Job job = await SeedJobAsync();
        (Guid existingId, byte[] rowVersion) = await SeedStepAsync(job.Id, "Existing", "RunDatabaseLoad");

        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new BulkUpdateJobStepsCommandHandler(context);

        var command = new BulkUpdateJobStepsCommand
        {
            JobId = job.Id,
            Steps =
            [
                new BulkUpdateStepItem
                {
                    StepId = existingId,
                    Name = "Updated",
                    StepType = "RunDatabaseLoad",
                    OnFailure = OnFailureAction.Stop,
                    IsEnabled = true,
                    RowVersion = rowVersion
                },
                new BulkUpdateStepItem
                {
                    StepId = Guid.NewGuid(),
                    Name = "Ghost",
                    StepType = "RunDatabaseLoad",
                    OnFailure = OnFailureAction.Stop,
                    IsEnabled = true,
                    RowVersion = [0, 0, 0, 0, 0, 0, 0, 1]
                }
            ]
        };

        Result<BulkOperationResponse<UpdateStepResult>> result =
            await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Succeeded.Count.ShouldBe(1);
        result.Value.Failed.Count.ShouldBe(1);
        result.Value.Failed[0].Index.ShouldBe(1);
    }

    // ── BulkDelete ──────────────────────────────────────────────────────────

    [Fact]
    public async Task BulkDeleteSteps_ShouldRemoveSteps()
    {
        Job job = await SeedJobAsync();
        (Guid id1, byte[] rv1) = await SeedStepAsync(job.Id, "S1", "RunDatabaseLoad");
        (Guid id2, byte[] rv2) = await SeedStepAsync(job.Id, "S2", "SendEmail");

        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new BulkDeleteJobStepsCommandHandler(context);

        var command = new BulkDeleteJobStepsCommand
        {
            JobId = job.Id,
            Steps =
            [
                new BulkDeleteStepItem { StepId = id1, RowVersion = rv1 },
                new BulkDeleteStepItem { StepId = id2, RowVersion = rv2 }
            ]
        };

        Result<BulkOperationResponse<StepDeleteResult>> result =
            await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Succeeded.Count.ShouldBe(2);
        result.Value.Failed.ShouldBeEmpty();

        await using ApplicationDbContext verify = fixture.CreateContext();
        Job? loaded = await verify.Jobs
            .Include(j => j.Steps)
            .FirstOrDefaultAsync(j => j.Id == job.Id);

        loaded!.Steps.ShouldNotContain(s => s.Id == id1 || s.Id == id2);
    }

    [Fact]
    public async Task BulkDeleteSteps_WithMixOfExistingAndMissing_ShouldPartiallySucceed()
    {
        Job job = await SeedJobAsync();
        (Guid existingId, byte[] rv) = await SeedStepAsync(job.Id, "Existing", "RunDatabaseLoad");

        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new BulkDeleteJobStepsCommandHandler(context);

        var command = new BulkDeleteJobStepsCommand
        {
            JobId = job.Id,
            Steps =
            [
                new BulkDeleteStepItem { StepId = existingId, RowVersion = rv },
                new BulkDeleteStepItem { StepId = Guid.NewGuid(), RowVersion = [0, 0, 0, 0, 0, 0, 0, 1] }
            ]
        };

        Result<BulkOperationResponse<StepDeleteResult>> result =
            await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Succeeded.Count.ShouldBe(1);
        result.Value.Failed.Count.ShouldBe(1);
    }

    [Fact]
    public async Task BulkDeleteSteps_ShouldReorderRemainingSteps()
    {
        Job job = await SeedJobAsync();
        (Guid id1, byte[] rv1) = await SeedStepAsync(job.Id, "Step1", "RunDatabaseLoad");
        await SeedStepAsync(job.Id, "Step2", "SendEmail");
        await SeedStepAsync(job.Id, "Step3", "RunDatabaseLoad");

        // Delete the first step — Step2 and Step3 should reorder to 1 and 2
        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new BulkDeleteJobStepsCommandHandler(context);

        var command = new BulkDeleteJobStepsCommand
        {
            JobId = job.Id,
            Steps = [new BulkDeleteStepItem { StepId = id1, RowVersion = rv1 }]
        };

        Result<BulkOperationResponse<StepDeleteResult>> result =
            await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        await using ApplicationDbContext verify = fixture.CreateContext();
        Job? loaded = await verify.Jobs
            .Include(j => j.Steps)
            .FirstOrDefaultAsync(j => j.Id == job.Id);

        loaded!.Steps.Count.ShouldBe(3); // initial step + Step2 + Step3
        List<JobStep> ordered = loaded.Steps.OrderBy(s => s.StepOrder).ToList();
        ordered[0].StepOrder.ShouldBe(1);
        ordered[1].StepOrder.ShouldBe(2);
        ordered[1].Name.ShouldBe("Step2");
        ordered[2].StepOrder.ShouldBe(3);
        ordered[2].Name.ShouldBe("Step3");
    }

    // ── Reorder ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReorderSteps_ShouldUpdateStepOrder()
    {
        Job job = await SeedJobAsync();
        Guid id0 = job.Steps[0].Id; // initial step from seed
        (Guid id1, _) = await SeedStepAsync(job.Id, "Step1", "RunDatabaseLoad");
        (Guid id2, _) = await SeedStepAsync(job.Id, "Step2", "SendEmail");
        (Guid id3, _) = await SeedStepAsync(job.Id, "Step3", "RunDatabaseLoad");

        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new ReorderJobStepsCommandHandler(context);

        // Reorder all 4: id3, id0, id1, id2
        var command = new ReorderJobStepsCommand
        {
            JobId = job.Id,
            StepIds = [id3, id0, id1, id2]
        };

        Result result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        await using ApplicationDbContext verify = fixture.CreateContext();
        Job? loaded = await verify.Jobs
            .Include(j => j.Steps)
            .FirstOrDefaultAsync(j => j.Id == job.Id);

        loaded!.Steps.Count.ShouldBe(4);
        List<JobStep> ordered = loaded.Steps.OrderBy(s => s.StepOrder).ToList();
        ordered[0].Id.ShouldBe(id3);
        ordered[0].StepOrder.ShouldBe(1);
        ordered[1].Id.ShouldBe(id0);
        ordered[1].StepOrder.ShouldBe(2);
        ordered[2].Id.ShouldBe(id1);
        ordered[2].StepOrder.ShouldBe(3);
        ordered[3].Id.ShouldBe(id2);
        ordered[3].StepOrder.ShouldBe(4);
    }

    [Fact]
    public async Task ReorderSteps_WhenCountMismatch_ShouldReturnFailure()
    {
        Job job = await SeedJobAsync();
        await SeedStepAsync(job.Id, "Step1", "RunDatabaseLoad");
        await SeedStepAsync(job.Id, "Step2", "SendEmail");

        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new ReorderJobStepsCommandHandler(context);

        // Provide right count (2) but wrong IDs
        var command = new ReorderJobStepsCommand
        {
            JobId = job.Id,
            StepIds = [Guid.NewGuid(), Guid.NewGuid()]
        };

        Result result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Job.ReorderStepCountMismatch");
    }

    [Fact]
    public async Task ReorderSteps_WhenStepNotFound_ShouldReturnFailure()
    {
        Job job = await SeedJobAsync();
        await SeedStepAsync(job.Id, "Step1", "RunDatabaseLoad");

        await using ApplicationDbContext context = fixture.CreateContext();
        var handler = new ReorderJobStepsCommandHandler(context);

        var command = new ReorderJobStepsCommand
        {
            JobId = job.Id,
            StepIds = [Guid.NewGuid(), Guid.NewGuid()] // right count (2), wrong IDs
        };

        Result result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Job.StepNotFound");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<Job> SeedJobAsync()
    {
        await using ApplicationDbContext context = fixture.CreateContext();

        Job job = Job.Create(
            $"Test Job {Guid.NewGuid():N}",
            null,
            [("Step 1", "RunDatabaseLoad", null, OnFailureAction.Stop)],
            [("Daily", "0 0 10 * * ?", "UTC")]).Value;

        context.Jobs.Add(job);
        await context.SaveChangesAsync();

        return job;
    }

    private async Task<(Guid Id, byte[] RowVersion)> SeedStepAsync(Guid jobId, string name, string stepType)
    {
        await using ApplicationDbContext context = fixture.CreateContext();

        Job job = await context.Jobs
            .Include(j => j.Steps)
            .SingleAsync(j => j.Id == jobId);

        Result<Guid> result = job.AddStep(name, stepType, null, OnFailureAction.Stop);
        await context.SaveChangesAsync();

        JobStep step = job.Steps.First(s => s.Id == result.Value);
        return (result.Value, step.RowVersion);
    }
}
