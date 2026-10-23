using Bogus;
using Mita.Domain.Groups;
using Shouldly;

namespace Mita.Api.UnitTests.Groups;

public sealed class MinorAllocationTests
{
    public static TheoryData<long, long[], int[], long[]> Rows => new()
    {
        { 10_000, [1, 1, 1], [1, 0, 2], [3_333, 3_334, 3_333] },
        { 3_070, [75_000, 75_000, 75_000, 75_000], [1, 0, 2, 3], [768, 768, 767, 767] },
        { 100, [1, 2], [0, 1], [33, 67] },
        { 0, [5, 5], [0, 1], [0, 0] },
        { 1, [1, 1, 1], [2, 0, 1], [0, 0, 1] },
        { 100_000_000_000, [100_000_000_000, 1], [0, 1], [99_999_999_999, 1] }
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void Should_AllocateByLargestRemainder(long total, long[] weights, int[] priority, long[] expected) =>
        MinorAllocation.Allocate(total, weights, priority).ShouldBe(expected);

    [Fact]
    public void Should_Throw_When_WeightsAreEmpty() =>
        Should.Throw<ArgumentException>(() => MinorAllocation.Allocate(100, [], []));

    [Fact]
    public void Should_Throw_When_WeightsSumToZero() =>
        Should.Throw<ArgumentException>(() => MinorAllocation.Allocate(100, [0, 0], [0, 1]));

    [Fact]
    public void Should_Throw_When_WeightIsNegative() =>
        Should.Throw<ArgumentException>(() => MinorAllocation.Allocate(100, [3, -1], [0, 1]));

    [Fact]
    public void Should_KeepTheTotal_ForRandomInputs()
    {
        var random = new Randomizer(20261023);

        for (var run = 0; run < 500; run++)
        {
            var total = random.Long(0, 100_000_000_000);
            var weights = Enumerable.Range(0, random.Int(1, 50))
                .Select(_ => random.Long(0, 100_000_000_000))
                .ToArray();
            if (weights.All(weight => weight == 0))
            {
                weights[0] = 1;
            }

            var priority = random.Shuffle(Enumerable.Range(0, weights.Length)).ToArray();

            var values = MinorAllocation.Allocate(total, weights, priority);

            values.Length.ShouldBe(weights.Length);
            values.Sum().ShouldBe(total);
            var weightSum = weights.Aggregate(Int128.Zero, (sum, weight) => sum + weight);
            for (var index = 0; index < weights.Length; index++)
            {
                var deviation = Int128.Abs(((Int128)values[index] * weightSum) - ((Int128)total * weights[index]));
                (deviation < weightSum).ShouldBeTrue($"run {run}, index {index}");
            }
        }
    }
}
