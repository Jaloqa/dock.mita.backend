using System.Text.Json;
using Mita.Api.Common.Json;
using Mita.Domain.Money;
using Mita.Domain.Users;

namespace Mita.Api.Common.Settings;

public static class SettingsWire
{
    public static string ToWire(Currency currency) => currency switch
    {
        Currency.PLN => "PLN",
        Currency.EUR => "EUR",
        Currency.USD => "USD",
        Currency.UAH => "UAH",
        _ => throw new ArgumentOutOfRangeException(nameof(currency), currency, null)
    };

    public static string ToWire(Locale locale) => locale switch
    {
        Locale.En => "en",
        Locale.Pl => "pl",
        Locale.Ru => "ru",
        Locale.Uk => "uk",
        _ => throw new ArgumentOutOfRangeException(nameof(locale), locale, null)
    };

    public static string ToWire(ThemePreference theme) => theme switch
    {
        ThemePreference.System => "system",
        ThemePreference.Light => "light",
        ThemePreference.Dark => "dark",
        _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, null)
    };

    public static bool IsMissing(JsonElement? element) =>
        element is null or { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined };

    public static bool TryParseCurrency(JsonElement? element, out Currency currency)
    {
        (currency, var ok) = Text(element) switch
        {
            "PLN" => (Currency.PLN, true),
            "EUR" => (Currency.EUR, true),
            "USD" => (Currency.USD, true),
            "UAH" => (Currency.UAH, true),
            _ => (default(Currency), false)
        };
        return ok;
    }

    public static bool TryParseLocale(JsonElement? element, out Locale locale)
    {
        (locale, var ok) = Text(element) switch
        {
            "en" => (Locale.En, true),
            "pl" => (Locale.Pl, true),
            "ru" => (Locale.Ru, true),
            "uk" => (Locale.Uk, true),
            _ => (default(Locale), false)
        };
        return ok;
    }

    public static bool TryParseTheme(JsonElement? element, out ThemePreference theme)
    {
        (theme, var ok) = Text(element) switch
        {
            "system" => (ThemePreference.System, true),
            "light" => (ThemePreference.Light, true),
            "dark" => (ThemePreference.Dark, true),
            _ => (default(ThemePreference), false)
        };
        return ok;
    }

    public static bool TryParseMonthStart(JsonElement? element, out MonthStart monthStart)
    {
        monthStart = default;
        switch (element)
        {
            case { ValueKind: JsonValueKind.String } text when text.GetString() == MonthStartJsonConverter.LastWorkingDayWireValue:
                monthStart = MonthStart.LastWorkingDay;
                return true;
            case { ValueKind: JsonValueKind.Number } number
                when number.TryGetDouble(out var value)
                    && value == Math.Floor(value)
                    && value is >= MonthStart.MinDay and <= MonthStart.MaxDay:
                monthStart = MonthStart.Day((int)value);
                return true;
            default:
                return false;
        }
    }

    public static bool TryParseNotifications(JsonElement? element, out NotificationPrefs notifications)
    {
        notifications = NotificationPrefs.Default;
        if (element is not { ValueKind: JsonValueKind.Object } value
            || !TryFlag(value, "budgetLimit", out var budgetLimit)
            || !TryFlag(value, "dailyReminder", out var dailyReminder)
            || !TryFlag(value, "weeklyReport", out var weeklyReport)
            || !TryFlag(value, "goals", out var goals))
        {
            return false;
        }

        notifications = new NotificationPrefs(budgetLimit, dailyReminder, weeklyReport, goals);
        return true;
    }

    private static string? Text(JsonElement? element) =>
        element is { ValueKind: JsonValueKind.String } text ? text.GetString() : null;

    private static bool TryFlag(JsonElement parent, string name, out bool flag)
    {
        flag = false;
        if (!parent.TryGetProperty(name, out var property)
            || property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        flag = property.GetBoolean();
        return true;
    }
}