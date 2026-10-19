using Mita.Domain.Users;

namespace Mita.Domain.Periods;

public readonly record struct FinancialPeriod(PeriodKey Key, DateOnly From, DateOnly To)
{
    public static FinancialPeriod ByKey(PeriodKey key, MonthStart start)
    {
        var from = StartOf(key, start);
        var to = StartOf(key.AddMonths(1), start).AddDays(-1);
        return new FinancialPeriod(key, from, to);
    }

    public static FinancialPeriod For(DateOnly date, MonthStart start)
    {
        var month = PeriodKey.Of(date);
        foreach (var offset in (ReadOnlySpan<int>)[0, -1, 1])
        {
            var period = ByKey(month.AddMonths(offset), start);
            if (date >= period.From && date <= period.To)
            {
                return period;
            }
        }

        throw new InvalidOperationException($"No financial period contains {date:yyyy-MM-dd}.");
    }

    public static FinancialPeriod Shift(PeriodKey key, int months, MonthStart start) =>
        ByKey(key.AddMonths(months), start);

    public static IReadOnlyList<FinancialPeriod> Last(DateOnly today, MonthStart start, int count)
    {
        var current = For(today, start).Key;
        var periods = new FinancialPeriod[count];
        for (var index = 0; index < count; index++)
        {
            periods[index] = Shift(current, index - count + 1, start);
        }

        return periods;
    }

    public int DaysLeft(DateOnly today)
    {
        if (today > To)
        {
            return 0;
        }

        var from = today < From ? From : today;
        return To.DayNumber - from.DayNumber + 1;
    }

    private static DateOnly StartOf(PeriodKey key, MonthStart start)
    {
        if (start.Value is { } day)
        {
            return new DateOnly(key.Year, key.Month, day);
        }

        var previous = key.AddMonths(-1);
        var last = new DateOnly(previous.Year, previous.Month, DateTime.DaysInMonth(previous.Year, previous.Month));
        while (last.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            last = last.AddDays(-1);
        }

        return last;
    }
}
