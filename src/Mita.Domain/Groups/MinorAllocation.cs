namespace Mita.Domain.Groups;

public static class MinorAllocation
{
    public static long[] Allocate(long total, IReadOnlyList<long> weights, IReadOnlyList<int> priority)
    {
        if (weights.Count == 0)
        {
            throw new ArgumentException("At least one weight is required.", nameof(weights));
        }

        if (weights.Any(weight => weight < 0))
        {
            throw new ArgumentException("A weight must not be negative.", nameof(weights));
        }

        var weightSum = weights.Aggregate(Int128.Zero, (sum, weight) => sum + weight);
        if (weightSum == 0)
        {
            throw new ArgumentException("The weights must not sum to zero.", nameof(weights));
        }

        var rank = Rank(priority, weights.Count);
        var values = new long[weights.Count];
        var remainders = new Int128[weights.Count];
        Int128 allocated = 0;
        for (var index = 0; index < weights.Count; index++)
        {
            var scaled = (Int128)total * weights[index];
            var quotient = scaled / weightSum;
            var remainder = scaled % weightSum;
            if (remainder < 0)
            {
                quotient--;
                remainder += weightSum;
            }

            values[index] = (long)quotient;
            remainders[index] = remainder;
            allocated += quotient;
        }

        var left = (int)(total - allocated);
        var receivers = Enumerable.Range(0, weights.Count)
            .OrderByDescending(index => remainders[index])
            .ThenBy(index => rank[index])
            .Take(left);
        foreach (var index in receivers)
        {
            values[index]++;
        }

        return values;
    }

    private static int[] Rank(IReadOnlyList<int> priority, int count)
    {
        if (priority.Count != count)
        {
            throw new ArgumentException("The priority must list every index once.", nameof(priority));
        }

        var rank = Enumerable.Repeat(-1, count).ToArray();
        for (var position = 0; position < count; position++)
        {
            var index = priority[position];
            if (index < 0 || index >= count || rank[index] >= 0)
            {
                throw new ArgumentException("The priority must list every index once.", nameof(priority));
            }

            rank[index] = position;
        }

        return rank;
    }
}
