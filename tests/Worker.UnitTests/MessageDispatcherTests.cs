namespace Worker.UnitTests;

/// <summary>
/// Unit tests for MessageDispatcher — the component that resolves a tenant, sets up the
/// DI scope, and routes each job message to the correct handler.
///
/// These tests use NSubstitute to isolate the dispatcher from real databases and ASB.
/// They focus on the defensive branches: unknown tenant, unknown message type.
/// The happy-path dispatch is covered by the integration tests in Worker.IntegrationTests.
/// </summary>
public sealed class MessageDispatcherTests
{
    private readonly ITenantRegistry _registry = Substitute.For<ITenantRegistry>();
    private readonly IServiceScopeFactory _scopeFactory = Substitute.For<IServiceScopeFactory>();

    private MessageDispatcher CreateDispatcher() =>
        new(_scopeFactory, _registry, NullLogger<MessageDispatcher>.Instance);

    // ── Tenant not found ──────────────────────────────────────────────────────

    [Fact]
    public async Task DispatchAsync_WhenTenantNotFound_ShouldReturnWithoutCreatingScope()
    {
        // Arrange — registry returns null, simulating a tenant that was deleted
        // or whose connection string hasn't been provisioned yet.
        _registry.GetConnectionStringAsync("missing").Returns((string?)null);

        // Act — should not throw; dispatcher logs and drops the message
        await CreateDispatcher().DispatchAsync(
            "missing",
            new JobMessage { MessageType = OutboxMessageTypes.DomainEvent, IdempotencyKey = Guid.NewGuid().ToString() },
            CancellationToken.None);

        // Assert — no scope was created, so no handler was invoked
        _scopeFactory.DidNotReceive().CreateScope();
    }

    // ── Unknown message type ──────────────────────────────────────────────────

    [Fact]
    public async Task DispatchAsync_WhenUnknownMessageType_ShouldNotInvokeDomainEventsDispatcher()
    {
        // Arrange — tenant resolves correctly, but the message carries an unrecognised type.
        // This guards against future message types being published before the worker is
        // updated to handle them.
        const string connectionString = "Server=.;Database=test;Trusted_Connection=True;";
        _registry.GetConnectionStringAsync("tenant-a").Returns(connectionString);

        IServiceScope scope = Substitute.For<IServiceScope>();
        IServiceProvider provider = Substitute.For<IServiceProvider>();
        TenantContext tenantContext = new();

        provider.GetService(typeof(TenantContext)).Returns(tenantContext);
        scope.ServiceProvider.Returns(provider);
        _scopeFactory.CreateScope().Returns(scope);

        IDomainEventsDispatcher eventsDispatcher = Substitute.For<IDomainEventsDispatcher>();
        provider.GetService(typeof(IDomainEventsDispatcher)).Returns(eventsDispatcher);

        // Act
        await CreateDispatcher().DispatchAsync(
            "tenant-a",
            new JobMessage { MessageType = "UnknownFutureType", IdempotencyKey = Guid.NewGuid().ToString() },
            CancellationToken.None);

        // Assert — domain events dispatcher was never called
        await eventsDispatcher
            .DidNotReceive()
            .DispatchAsync(Arg.Any<IEnumerable<SharedKernel.IDomainEvent>>(), Arg.Any<CancellationToken>());
    }

    // ── Tenant context population ─────────────────────────────────────────────

    [Fact]
    public async Task DispatchAsync_ShouldPopulateTenantContext_BeforeDispatching()
    {
        // Arrange — verifies that TenantContext is populated with the correct tenant
        // ID and connection string before the handler scope is entered. Handlers depend
        // on this to resolve ApplicationDbContext against the right tenant database.
        const string connectionString = "Server=.;Database=test;Trusted_Connection=True;";
        _registry.GetConnectionStringAsync("tenant-b").Returns(connectionString);

        IServiceScope scope = Substitute.For<IServiceScope>();
        IServiceProvider provider = Substitute.For<IServiceProvider>();
        TenantContext tenantContext = new();

        provider.GetService(typeof(TenantContext)).Returns(tenantContext);
        scope.ServiceProvider.Returns(provider);
        _scopeFactory.CreateScope().Returns(scope);

        // Act — use an unknown type so dispatch exits early without needing
        // a full IDomainEventsDispatcher setup
        await CreateDispatcher().DispatchAsync(
            "tenant-b",
            new JobMessage { MessageType = "SomeType", IdempotencyKey = Guid.NewGuid().ToString() },
            CancellationToken.None);

        // Assert — TenantContext was populated before dispatch
        tenantContext.TenantId.ShouldBe("tenant-b");
        tenantContext.ConnectionString.ShouldBe(connectionString);
    }
}
