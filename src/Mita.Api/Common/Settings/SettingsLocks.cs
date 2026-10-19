using Microsoft.EntityFrameworkCore;
using Mita.Domain.Money;
using Mita.Domain.Users;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Settings;

public static class SettingsLocks
{
    public static async Task<(Currency BaseCurrency, MonthStart MonthStart)> LockSettingsAsync(
        this AppDbContext db,
        Guid userId,
        CancellationToken ct)
    {
        await db.Database
            .SqlQuery<string>($"""SELECT "BaseCurrency" AS "Value" FROM user_settings WHERE "UserId" = {userId} FOR SHARE""")
            .ToListAsync(ct);
        return await db.SettingsAsync(userId, ct);
    }
}
