using Application.Abstractions.Authentication;
using Testcontainers.MsSql;

namespace Application.IntegrationTests.Fixtures;

public sealed class ApplicationSqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder().Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        ConnectionString = _container.GetConnectionString();

        await using ApplicationDbContext context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public ApplicationDbContext CreateContext()
    {
        var interceptor = new AuditableEntityInterceptor(
            new TestUserContext(),
            TimeProvider.System);

        return new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString, sql =>
                sql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.App))
            .AddInterceptors(interceptor)
            .LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information)
            .EnableSensitiveDataLogging()
            .Options);
    }

    private sealed class TestUserContext : IUserContext
    {
        public Guid? UserId { get; } = Guid.Parse("00000000-0000-0000-0000-000000000001");
    }
}
