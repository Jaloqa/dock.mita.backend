using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Settings;
using Mita.Domain.Users;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Settings.GetSettings;

public sealed class GetSettingsHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetSettingsQuery, Result<SettingsDto>>
{
    public async Task<Result<SettingsDto>> Handle(GetSettingsQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var row = await db.Set<UserSettings>().AsNoTracking()
            .Where(settings => settings.UserId == userId)
            .Select(settings => new
            {
                settings.BaseCurrency,
                settings.Locale,
                settings.MonthStartDay,
                settings.Theme,
                settings.Notifications
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return Error.NotFound();
        }

        return new SettingsDto(
            SettingsWire.ToWire(row.BaseCurrency),
            SettingsWire.ToWire(row.Locale),
            row.MonthStartDay is { } day ? MonthStart.Day(day) : MonthStart.LastWorkingDay,
            SettingsWire.ToWire(row.Theme),
            new NotificationsDto(
                row.Notifications.BudgetLimit,
                row.Notifications.DailyReminder,
                row.Notifications.WeeklyReport,
                row.Notifications.Goals));
    }
}