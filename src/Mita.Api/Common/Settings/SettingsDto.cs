using System.Text.Json.Serialization;
using Mita.Api.Common.Json;
using Mita.Domain.Users;

namespace Mita.Api.Common.Settings;

public sealed record NotificationsDto(bool BudgetLimit, bool DailyReminder, bool WeeklyReport, bool Goals);

public sealed record SettingsDto(
    string BaseCurrency,
    string Locale,
    [property: JsonConverter(typeof(MonthStartJsonConverter))] MonthStart MonthStartDay,
    string Theme,
    NotificationsDto Notifications)
{
    public static SettingsDto From(UserSettings settings) => new(
        SettingsWire.ToWire(settings.BaseCurrency),
        SettingsWire.ToWire(settings.Locale),
        settings.MonthStart,
        SettingsWire.ToWire(settings.Theme),
        new NotificationsDto(
            settings.Notifications.BudgetLimit,
            settings.Notifications.DailyReminder,
            settings.Notifications.WeeklyReport,
            settings.Notifications.Goals));
}