using Bogus;
using Mita.Domain.Groups;
using Shouldly;

namespace Mita.Api.UnitTests.Groups;

public sealed class GroupSettlementTests
{
    [Fact]
    public void Should_PayTheLargestCreditor_When_NoBalancesMatch()
    {
        var (anna, olya, max, kasia) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var transfers = GroupSettlement.Suggest([(anna, 4_232), (olya, -698), (max, -1_767), (kasia, -1_767)]);

        transfers.ShouldBe(
            new[]
            {
                new GroupTransfer(max, anna, 1_767),
                new GroupTransfer(kasia, anna, 1_767),
                new GroupTransfer(olya, anna, 698)
            });
    }

    [Fact]
    public void Should_SettleExactPairsFirst()
    {
        var (a, b, c, d, e) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var transfers = GroupSettlement.Suggest([(a, 400), (b, 300), (c, 300), (d, -600), (e, -400)]);

        transfers.ShouldBe(
            new[]
            {
                new GroupTransfer(e, a, 400),
                new GroupTransfer(d, b, 300),
                new GroupTransfer(d, c, 300)
            });
    }

    [Fact]
    public void Should_SuggestNothing_When_EveryoneIsSettled()
    {
        var transfers = GroupSettlement.Suggest([(Guid.NewGuid(), 0), (Guid.NewGuid(), 0), (Guid.NewGuid(), 0)]);

        transfers.ShouldBeEmpty();
    }

    [Fact]
    public void Should_Throw_When_BalancesDoNotSumToZero() =>
        Should.Throw<ArgumentException>(() => GroupSettlement.Suggest([(Guid.NewGuid(), 1), (Guid.NewGuid(), 0)]));

    [Fact]
    public void Should_SettleEveryone_ForRandomBalances()
    {
        var random = new Randomizer(20261023);

        for (var run = 0; run < 300; run++)
        {
            var spread = run % 2 == 0 ? 30L : 1_000_000L;
            var members = Enumerable.Range(0, random.Int(2, 12)).Select(_ => Guid.NewGuid()).ToArray();
            var amounts = members.Skip(1).Select(_ => random.Long(-spread, spread)).ToList();
            amounts.Insert(0, -amounts.Sum());
            var balances = members.Zip(amounts, (member, amount) => (MemberId: member, Balance: amount)).ToList();

            var transfers = GroupSettlement.Suggest(balances);

            var remaining = balances.ToDictionary(balance => balance.MemberId, balance => balance.Balance);
            foreach (var transfer in transfers)
            {
                transfer.Amount.ShouldBeGreaterThan(0, $"run {run}");
                remaining[transfer.FromMemberId] += transfer.Amount;
                remaining[transfer.ToMemberId] -= transfer.Amount;
            }

            remaining.Values.ShouldAllBe(balance => balance == 0, $"run {run}");
            var nonZero = balances.Count(balance => balance.Balance != 0);
            transfers.Count.ShouldBeLessThanOrEqualTo(Math.Max(0, nonZero - 1), $"run {run}");
        }
    }
}
