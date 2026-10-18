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

    public async ValueTask InitializeAsync() => await container.StartAsync();

    public async ValueTask DisposeAsync() => await container.DisposeAsync();
}
