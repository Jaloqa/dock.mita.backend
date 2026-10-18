using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Settings;
using Mita.Domain.Users;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Settings.UpdateSettings;

public sealed class UpdateSettingsHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<UpdateSettingsCommand, Result<SettingsDto>>
{
    public async Task<Result<SettingsDto>> Handle(UpdateSettingsCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var settings = await db.Set<UserSettings>().FirstOrDefaultAsync(row => row.UserId == userId, cancellationToken);
        if (settings is null)
        {
            return Error.NotFound();
        }

        if (!SettingsWire.TryParseCurrency(command.BaseCurrency, out var currency)
            || !SettingsWire.TryParseLocale(command.Locale, out var locale)
            || !SettingsWire.TryParseMonthStart(command.MonthStartDay, out var monthStart)
            || !SettingsWire.TryParseTheme(command.Theme, out var theme)
            || !SettingsWire.TryParseNotifications(command.Notifications, out var notifications))
        {
            throw new InvalidOperationException("The settings command reached the handler without passing validation.");
        }

        settings.BaseCurrency = currency;
        settings.Locale = locale;
        settings.MonthStart = monthStart;
        settings.Theme = theme;
        settings.Notifications = notifications;

        await db.SaveChangesAsync(cancellationToken);

        return SettingsDto.From(settings);
    }
}