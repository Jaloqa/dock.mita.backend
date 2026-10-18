using Mita.Domain.Money;

namespace Mita.Domain.Users;

public sealed class UserSettings
{
    public Guid UserId { get; private set; }

    public Currency BaseCurrency { get; set; }

    public Locale Locale { get; set; }

    public int? MonthStartDay { get; private set; } = 1;

    public ThemePreference Theme { get; set; }

    public NotificationPrefs Notifications { get; set; } = NotificationPrefs.Default;

    public MonthStart MonthStart
    {
        get => MonthStartDay is { } day ? MonthStart.Day(day) : MonthStart.LastWorkingDay;
        set => MonthStartDay = value.Value;
    }

    public static UserSettings CreateDefault(Guid userId, Locale locale) => new()
    {
        UserId = userId,
        BaseCurrency = Currency.PLN,
        Locale = locale,
        MonthStart = MonthStart.Day(1),
        Theme = ThemePreference.System,
        Notifications = NotificationPrefs.Default
    };
}
