using Microsoft.EntityFrameworkCore;
using Mita.Domain.Money;
using Mita.Domain.Users;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Settings;

public static class SettingsQueries
{
    public static async Task<(Currency BaseCurrency, MonthStart MonthStart)> SettingsAsync(
        this AppDbContext db,
        Guid userId,
        CancellationToken ct)
    {
        var settings = await db.Set<UserSettings>().AsNoTracking()
            .SingleAsync(row => row.UserId == userId, ct);
        return (settings.BaseCurrency, settings.MonthStart);
    }
}
