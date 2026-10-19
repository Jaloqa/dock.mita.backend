namespace Mita.Api.Common.Reporting;

public static class Shares
{
    private const int Whole = 1000;

    public static int[] Tenths(IReadOnlyList<long> amounts)
    {
        var tenths = new int[amounts.Count];
        Int128 total = 0;
        foreach (var amount in amounts)
        {
            total += amount;
        }

        if (total <= 0)
        {
            return tenths;
        }

        var remainders = new Int128[amounts.Count];
        var assigned = 0;
        for (var index = 0; index < amounts.Count; index++)
        {
            Int128 scaled = (Int128)amounts[index] * Whole;
            tenths[index] = (int)(scaled / total);
            remainders[index] = scaled % total;
            assigned += tenths[index];
        }

        var byRemainder = Enumerable.Range(0, amounts.Count)
            .OrderByDescending(index => remainders[index])
            .ThenBy(index => index);
        foreach (var index in byRemainder)
        {
            if (assigned >= Whole)
            {
                break;
            }

            tenths[index]++;
            assigned++;
        }

        return tenths;
    }

    public static double Percent(long tenths) => tenths == 0 ? 0 : tenths / 10.0;

    public static long RoundedAverage(long total, int count)
    {
        var magnitude = ((Int128)2 * Int128.Abs(total) + count) / ((Int128)2 * count);
        return (long)(total < 0 ? -magnitude : magnitude);
    }
}
