using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Extensions;

public static class MigrationExtensions
{
    public static async Task ApplyMitaMigrationsAsync(this WebApplication app, CancellationToken ct = default)
    {
        if (!app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value.ApplyMigrationsOnStartup)
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync(ct);
    }
}
