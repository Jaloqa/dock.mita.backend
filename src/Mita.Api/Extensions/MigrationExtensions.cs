using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Microsoft.Extensions.Options;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Extensions;

public static class MigrationExtensions
{
    private const long MigrationLockKey = 7362543157;

    public static async Task ApplyMitaMigrationsAsync(this WebApplication app, CancellationToken ct = default)
    {
        if (!app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value.ApplyMigrationsOnStartup)
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database;
        await EnsureDatabaseExistsAsync(database, ct);
        await database.OpenConnectionAsync(ct);
        database.SetCommandTimeout(TimeSpan.FromMinutes(5));
        try
        {
            await database.ExecuteSqlRawAsync("SELECT pg_advisory_lock({0})", [MigrationLockKey], ct);
            try
            {
                await database.MigrateAsync(ct);
            }
            finally
            {
                await database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock({0})", [MigrationLockKey], CancellationToken.None);
            }
        }
        finally
        {
            await database.CloseConnectionAsync();
        }
    }

    private static async Task EnsureDatabaseExistsAsync(DatabaseFacade database, CancellationToken ct)
    {
        if (await database.CanConnectAsync(ct))
        {
            return;
        }

        try
        {
            await database.GetService<IRelationalDatabaseCreator>().CreateAsync(ct);
        }
        catch (PostgresException exception) when (
            exception.SqlState is PostgresErrorCodes.DuplicateDatabase or PostgresErrorCodes.UniqueViolation)
        {
        }
    }
}
