namespace Mita.Api.Common.Time;

public static class UtcCalendar
{
    public static DateOnly Today(TimeProvider time) => DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);

    public static DateOnly LatestAllowedDate(TimeProvider time) => Today(time).AddYears(1);
}
