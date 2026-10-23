using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Categories;
using Mita.Domain.Users;
using Mita.Infrastructure.Persistence;
using Npgsql;
using Shouldly;

namespace Mita.Api.IntegrationTests.Persistence;

public sealed class MigrationTests(PostgresFixture postgres, MitaApiFactory factory) : IClassFixture<MitaApiFactory>
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
            WHERE table_schema = 'public' AND table_name IN ('AspNetUsers', 'user_settings', 'refresh_tokens',
                'accounts', 'transfers', 'exchange_rates', 'account_movements', 'categories', 'transactions',
                'budgets', 'goals', 'goal_contributions', 'tags', 'transaction_tags', 'debts', 'debt_repayments',
                'groups', 'group_members', 'group_categories', 'group_expenses', 'group_expense_shares',
                'group_repayments', 'group_entry_events')
            """,
            connection);
        var tables = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                tables.Add(reader.GetString(0));
            }
        }

        tables.ShouldBe(
            [
                "AspNetUsers", "refresh_tokens", "user_settings", "accounts", "transfers", "exchange_rates",
                "account_movements", "categories", "transactions", "budgets", "goals", "goal_contributions", "tags",
                "transaction_tags", "debts", "debt_repayments", "groups", "group_members", "group_categories",
                "group_expenses", "group_expense_shares", "group_repayments", "group_entry_events"
            ],
            ignoreOrder: true);

        await using var function = new NpgsqlCommand(
            "SELECT count(*) FROM pg_proc WHERE proname = 'pln_value'",
            connection);
        ((long)(await function.ExecuteScalarAsync(ct))!).ShouldBe(1);

        await using var rateFunctions = new NpgsqlCommand(
            "SELECT count(*) FROM pg_proc WHERE proname IN ('currency_exponent', 'round_rate')",
            connection);
        ((long)(await rateFunctions.ExecuteScalarAsync(ct))!).ShouldBe(2);

        await using var rateColumns = new NpgsqlCommand(
            """
            SELECT table_name || '.' || column_name || ' ' || data_type || '(' || numeric_precision || ',' || numeric_scale || ')'
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND ((table_name = 'exchange_rates' AND column_name = 'PlnValue')
                OR (table_name IN ('transactions', 'transfers', 'group_expenses', 'group_repayments')
                    AND column_name = 'Rate'))
            """,
            connection);
        var columns = new List<string>();
        await using (var reader = await rateColumns.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                columns.Add(reader.GetString(0));
            }
        }

        columns.ShouldBe(
            [
                "exchange_rates.PlnValue numeric(24,12)", "transactions.Rate numeric(24,12)",
                "transfers.Rate numeric(24,12)", "group_expenses.Rate numeric(24,12)",
                "group_repayments.Rate numeric(24,12)"
            ],
            ignoreOrder: true);

        await using var groupColumns = new NpgsqlCommand(
            """
            SELECT table_name || '.' || column_name || ' ' || udt_name
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND ((table_name = 'group_expenses' AND column_name = 'Tags')
                OR (table_name = 'group_entry_events' AND column_name = 'State'))
            """,
            connection);
        var groupTypes = new List<string>();
        await using (var reader = await groupColumns.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                groupTypes.Add(reader.GetString(0));
            }
        }

        groupTypes.ShouldBe(["group_expenses.Tags _text", "group_entry_events.State jsonb"], ignoreOrder: true);

        await using var index = new NpgsqlCommand(
            "SELECT count(*) FROM pg_indexes WHERE indexname = 'IX_tags_user_name'",
            connection);
        ((long)(await index.ExecuteScalarAsync(ct))!).ShouldBe(1);
    }

    [Fact]
    public async Task Should_BackfillStarterCategories_When_UserExistedBeforeMigration()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = postgres.NewDatabaseConnectionString();
        await using var context = NewContext(connectionString);
        await context.GetService<IMigrator>().MigrateAsync("AccountsTransfersRates", cancellationToken: ct);
        var users = new[] { Locale.Pl, Locale.Uk, Locale.En, Locale.Ru }
            .Select(locale => (UserId: Guid.NewGuid(), Locale: locale))
            .ToList();
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync(ct);
            foreach (var (userId, locale) in users)
            {
                await InsertUserWithSettings(connection, userId, locale.ToString(), ct);
            }
        }

        await context.Database.MigrateAsync(ct);

        foreach (var (userId, locale) in users)
        {
            var stored = await context.Categories.AsNoTracking()
                .Where(category => category.UserId == userId)
                .OrderBy(category => category.Position)
                .Select(category => new { category.Name, category.Kind, category.Color, category.System, category.Position })
                .ToListAsync(ct);
            var expected = StarterCategories.For(userId, locale)
                .Select(category => new { category.Name, category.Kind, category.Color, category.System, category.Position })
                .ToList();

            stored.Count.ShouldBe(18);
            stored.ShouldBe(expected);
            stored.Select(category => category.Position).ShouldBe(Enumerable.Range(0, 18));
        }
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

    [Fact]
    public async Task Should_MigrateOnce_When_TwoHostsStartTogether()
    {
        var ct = TestContext.Current.CancellationToken;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var connectionString = postgres.NewDatabaseConnectionString();
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Mita"] = connectionString,
                ["Redis:InstanceName"] = $"mita-{Guid.NewGuid():N}:",
                ["Database:ApplyMigrationsOnStartup"] = "true"
            };
            await using var first = factory.WithSettings(settings);
            await using var second = factory.WithSettings(settings);

            var clients = await Task.WhenAll(
                Task.Run(first.CreateClient, ct),
                Task.Run(second.CreateClient, ct));

            foreach (var client in clients)
            {
                using var response = await client.GetAsync("/health/ready", ct);
                response.StatusCode.ShouldBe(HttpStatusCode.OK);
            }

            await using var context = NewContext(connectionString);
            var applied = await context.Database.GetAppliedMigrationsAsync(ct);
            var recorded = new List<string>();
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand(
                """SELECT "MigrationId" FROM "__EFMigrationsHistory" """,
                connection);
            await using (var reader = await command.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    recorded.Add(reader.GetString(0));
                }
            }

            recorded.ShouldBe(applied, ignoreOrder: true);
            recorded.ShouldBe(recorded.Distinct());
            recorded.Count.ShouldBe(context.Database.GetMigrations().Count());
        }
    }

    private static AppDbContext NewContext(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);

    private static async Task Insert(NpgsqlConnection connection, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(InsertUser, connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task InsertUserWithSettings(
        NpgsqlConnection connection,
        Guid userId,
        string locale,
        CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO "AspNetUsers" ("Id", "Name", "NormalizedEmail", "EmailConfirmed", "PhoneNumberConfirmed",
                "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount", "CreatedAt", "UpdatedAt")
            VALUES (@id, 'Anna', @email, false, false, false, true, 0, now(), now());
            INSERT INTO user_settings ("UserId", "BaseCurrency", "Locale", "MonthStartDay", "Theme",
                "NotifyBudgetLimit", "NotifyDailyReminder", "NotifyGoals", "NotifyWeeklyReport")
            VALUES (@id, 'PLN', @locale, 1, 'System', true, false, true, true);
            """,
            connection);
        command.Parameters.AddWithValue("id", userId);
        command.Parameters.AddWithValue("email", $"{userId:N}@EXAMPLE.COM");
        command.Parameters.AddWithValue("locale", locale);
        await command.ExecuteNonQueryAsync(ct);
    }
}
