using Microsoft.EntityFrameworkCore;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Infrastructure.Persistence;
using Npgsql;
using Shouldly;

namespace Mita.Api.IntegrationTests.Persistence;

public sealed class MigrationTests(PostgresFixture postgres)
{
    private const string InsertUser =
        """
        INSERT INTO "AspNetUsers" ("Id", "Name", "NormalizedEmail", "EmailConfirmed", "PhoneNumberConfirmed",
            "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount", "CreatedAt", "UpdatedAt")
        VALUES (@id, 'Anna', 'ANNA@EXAMPLE.COM', false, false, false, true, 0, now(), now())
        """;

    [Fact]
    public async Task Should_ApplyMigrations_When_DatabaseIsEmpty()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = postgres.NewDatabaseConnectionString();
        await using var context = NewContext(connectionString);

        await context.Database.MigrateAsync(ct);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            """
            SELECT table_name FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name IN ('AspNetUsers', 'user_settings', 'refresh_tokens')
            """,
            connection);
        var tables = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            tables.Add(reader.GetString(0));
        }

        tables.ShouldBe(["AspNetUsers", "refresh_tokens", "user_settings"], ignoreOrder: true);
    }

    [Fact]
    public async Task Should_RejectSecondUser_When_NormalizedEmailRepeats()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = postgres.NewDatabaseConnectionString();
        await using var context = NewContext(connectionString);
        await context.Database.MigrateAsync(ct);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await Insert(connection, ct);
        var duplicate = await Should.ThrowAsync<PostgresException>(() => Insert(connection, ct));

        duplicate.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    private static AppDbContext NewContext(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);

    private static async Task Insert(NpgsqlConnection connection, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(InsertUser, connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        await command.ExecuteNonQueryAsync(ct);
    }
}
