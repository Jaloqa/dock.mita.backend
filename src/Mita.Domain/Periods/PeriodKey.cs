using System.Globalization;
using System.Text.RegularExpressions;

namespace Mita.Domain.Periods;

public readonly partial record struct PeriodKey(int Year, int Month) : IComparable<PeriodKey>
{
    public int MonthIndex => Year * 12 + Month;

    public static bool TryParse(string? text, out PeriodKey key)
    {
        key = default;
        if (text is null || !KeyPattern().IsMatch(text))
        {
            return false;
        }

        key = new PeriodKey(
            int.Parse(text.AsSpan(0, 4), CultureInfo.InvariantCulture),
            int.Parse(text.AsSpan(5, 2), CultureInfo.InvariantCulture));
        return true;
    }

    public static PeriodKey Of(DateOnly date) => new(date.Year, date.Month);

    public PeriodKey AddMonths(int months)
    {
        var index = MonthIndex - 1 + months;
        return new PeriodKey(index / 12, index % 12 + 1);
    }

    public int CompareTo(PeriodKey other) => MonthIndex.CompareTo(other.MonthIndex);

    public static bool operator <(PeriodKey left, PeriodKey right) => left.CompareTo(right) < 0;

    public static bool operator <=(PeriodKey left, PeriodKey right) => left.CompareTo(right) <= 0;

    public static bool operator >(PeriodKey left, PeriodKey right) => left.CompareTo(right) > 0;

    public static bool operator >=(PeriodKey left, PeriodKey right) => left.CompareTo(right) >= 0;

    public override string ToString() => $"{Year:D4}-{Month:D2}";

    [GeneratedRegex(@"\A(?:19[7-9][0-9]|20[0-9]{2})-(?:0[1-9]|1[0-2])\z")]
    private static partial Regex KeyPattern();
}
