using Testcontainers.MsSql;

namespace Worker.IntegrationTests.Fixtures;

/// <summary>
/// Starts a real SQL Server in Docker once per test class and applies the application
/// schema via EnsureCreatedAsync. Shared across all tests in the class via IClassFixture
/// to avoid the cost of spinning up a new container per test.
/// </summary>
public sealed class WorkerSqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder().Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        ConnectionString = _container.GetConnectionString();

        // EnsureCreatedAsync creates all tables from the EF model without running
        // migrations — correct for tests where we care about schema shape, not history.
        await using ApplicationDbContext context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public ApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(ConnectionString, sql =>
                    sql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.App))
                .Options);
}
