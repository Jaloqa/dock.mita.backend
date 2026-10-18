using Microsoft.EntityFrameworkCore;
using Mita.Infrastructure.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Mita.Api.IntegrationTests.Infrastructure.PostgresFixture))]

namespace Mita.Api.IntegrationTests.Infrastructure;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public string ConnectionString => container.GetConnectionString();

    public string NewDatabaseConnectionString() =>
        new NpgsqlConnectionStringBuilder(ConnectionString) { Database = $"t_{Guid.NewGuid():N}" }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync();

        await using var context = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options);
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await container.DisposeAsync();
}
