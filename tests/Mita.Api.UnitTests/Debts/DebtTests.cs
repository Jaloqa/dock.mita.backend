using System.Globalization;
using Mita.Domain.Debts;
using Mita.Domain.Money;
using Shouldly;

namespace Mita.Api.UnitTests.Debts;

public sealed class DebtTests
{
    private static readonly DateOnly Date = new(2026, 10, 1);

    private static readonly DateOnly Today = new(2026, 10, 23);

    [Fact]
    public void Should_BeOpenWithFullRemaining_When_DebtCreated()
    {
        var debt = Debt.Create(Guid.NewGuid(), Details(500));

        debt.Id.ShouldNotBe(Guid.Empty);
        debt.Amount.ShouldBe(500);
        debt.Repaid.ShouldBe(0);
        debt.Remaining.ShouldBe(500);
        debt.Status.ShouldBe(DebtStatus.Open);
    }

    [Fact]
    public void Should_ReduceRemaining_When_Repaid()
    {
        var debt = Debt.Create(Guid.NewGuid(), Details(500));

        debt.Repay(300);

        debt.Repaid.ShouldBe(300);
        debt.Remaining.ShouldBe(200);
        debt.Status.ShouldBe(DebtStatus.Open);
    }

    [Fact]
    public void Should_Throw_When_RepaymentExceedsRemaining()
    {
        var debt = Debt.Create(Guid.NewGuid(), Details(500));
        debt.Repay(300);

        Should.Throw<InvalidOperationException>(() => debt.Repay(201));

        debt.Repaid.ShouldBe(300);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Should_Throw_When_RepaymentIsNotPositive(long amount)
    {
        var debt = Debt.Create(Guid.NewGuid(), Details(500));

        Should.Throw<InvalidOperationException>(() => debt.Repay(amount));
    }

    [Fact]
    public void Should_BeSettled_When_RepaidInFull()
    {
        var debt = Debt.Create(Guid.NewGuid(), Details(500));
        debt.Repay(300);

        debt.Repay(200);

        debt.Remaining.ShouldBe(0);
        debt.Status.ShouldBe(DebtStatus.Settled);
    }

    [Fact]
    public void Should_RestoreRemaining_When_Unrepaid()
    {
        var debt = Debt.Create(Guid.NewGuid(), Details(500));
        debt.Repay(500);

        debt.Unrepay(300);

        debt.Repaid.ShouldBe(200);
        debt.Remaining.ShouldBe(300);
        debt.Status.ShouldBe(DebtStatus.Open);
    }

    [Fact]
    public void Should_Throw_When_UnrepaidMoreThanRepaid()
    {
        var debt = Debt.Create(Guid.NewGuid(), Details(500));
        debt.Repay(300);

        Should.Throw<InvalidOperationException>(() => debt.Unrepay(301));

        debt.Repaid.ShouldBe(300);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Should_Throw_When_UnrepaymentIsNotPositive(long amount)
    {
        var debt = Debt.Create(Guid.NewGuid(), Details(500));
        debt.Repay(300);

        Should.Throw<InvalidOperationException>(() => debt.Unrepay(amount));
    }

    [Fact]
    public void Should_Throw_When_AmountDropsBelowRepaid()
    {
        var debt = Debt.Create(Guid.NewGuid(), Details(500));
        debt.Repay(300);

        Should.Throw<ArgumentException>(() => debt.Update(Details(299)));

        debt.Amount.ShouldBe(500);
    }

    [Fact]
    public void Should_Update_When_AmountEqualsRepaid()
    {
        var debt = Debt.Create(Guid.NewGuid(), Details(500));
        debt.Repay(300);

        debt.Update(Details(300));

        debt.Amount.ShouldBe(300);
        debt.Status.ShouldBe(DebtStatus.Settled);
    }

    [Fact]
    public void Should_Throw_When_CurrencyChangesAfterRepayment()
    {
        var debt = Debt.Create(Guid.NewGuid(), Details(500));
        debt.Repay(100);

        Should.Throw<ArgumentException>(() => debt.Update(Details(500, currency: Currency.EUR)));

        debt.Currency.ShouldBe(Currency.PLN);
    }

    [Fact]
    public void Should_ChangeCurrency_When_NothingRepaid()
    {
        var debt = Debt.Create(Guid.NewGuid(), Details(500));

        debt.Update(Details(500, currency: Currency.EUR));

        debt.Currency.ShouldBe(Currency.EUR);
    }

    [Fact]
    public void Should_ReplaceEveryField_When_Updated()
    {
        var accountId = Guid.NewGuid();
        var debt = Debt.Create(Guid.NewGuid(), Details(500));

        debt.Update(new DebtDetails(
            DebtDirection.Borrowed,
            "Mum",
            2_000,
            Currency.EUR,
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 12, 1),
            accountId,
            8_400,
            4.2m,
            "Rent"));

        debt.Direction.ShouldBe(DebtDirection.Borrowed);
        debt.Counterparty.ShouldBe("Mum");
        debt.Amount.ShouldBe(2_000);
        debt.Currency.ShouldBe(Currency.EUR);
        debt.Date.ShouldBe(new DateOnly(2026, 9, 1));
        debt.DueDate.ShouldBe(new DateOnly(2026, 12, 1));
        debt.AccountId.ShouldBe(accountId);
        debt.AccountAmount.ShouldBe(8_400);
        debt.Rate.ShouldBe(4.2m);
        debt.Note.ShouldBe("Rent");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Should_Throw_When_AmountIsNotPositive(long amount)
    {
        Should.Throw<ArgumentException>(() => Debt.Create(Guid.NewGuid(), Details(amount)));
    }

    [Fact]
    public void Should_Throw_When_DueDateIsBeforeDate()
    {
        Should.Throw<ArgumentException>(() => Debt.Create(Guid.NewGuid(), Details(500, dueDate: Date.AddDays(-1))));
    }

    [Fact]
    public void Should_Create_When_DueDateEqualsDate()
    {
        var debt = Debt.Create(Guid.NewGuid(), Details(500, dueDate: Date));

        debt.DueDate.ShouldBe(Date);
    }

    [Fact]
    public void Should_Throw_When_AccountWithoutAccountAmount()
    {
        Should.Throw<ArgumentException>(() => Debt.Create(
            Guid.NewGuid(),
            Details(500, accountId: Guid.NewGuid(), accountAmount: null, rate: 1m)));
    }

    [Fact]
    public void Should_Throw_When_AccountWithoutRate()
    {
        Should.Throw<ArgumentException>(() => Debt.Create(
            Guid.NewGuid(),
            Details(500, accountId: Guid.NewGuid(), accountAmount: 500, rate: null)));
    }

    [Fact]
    public void Should_Throw_When_AccountAmountWithoutAccount()
    {
        Should.Throw<ArgumentException>(() => Debt.Create(
            Guid.NewGuid(),
            Details(500, accountId: null, accountAmount: 500, rate: null)));
    }

    [Fact]
    public void Should_Throw_When_RateWithoutAccount()
    {
        Should.Throw<ArgumentException>(() => Debt.Create(
            Guid.NewGuid(),
            Details(500, accountId: null, accountAmount: null, rate: 1m)));
    }

    [Fact]
    public void Should_Create_When_AccountFieldsAreAllSet()
    {
        var accountId = Guid.NewGuid();

        var debt = Debt.Create(Guid.NewGuid(), Details(500, accountId: accountId, accountAmount: 500, rate: 1m));

        debt.AccountId.ShouldBe(accountId);
        debt.AccountAmount.ShouldBe(500);
        debt.Rate.ShouldBe(1m);
    }

    [Theory]
    [InlineData("2026-10-22", 0, true)]
    [InlineData("2026-10-23", 0, false)]
    [InlineData("2026-10-22", 500, false)]
    [InlineData(null, 0, false)]
    public void Should_ComputeOverdue_When_DueDateIsPast(string? dueDate, long repaid, bool expected)
    {
        var debt = Debt.Create(
            Guid.NewGuid(),
            Details(
                500,
                date: new DateOnly(2026, 10, 1),
                dueDate: dueDate is null ? null : DateOnly.ParseExact(dueDate, "yyyy-MM-dd", CultureInfo.InvariantCulture)));
        if (repaid > 0)
        {
            debt.Repay(repaid);
        }

        debt.IsOverdue(Today).ShouldBe(expected);
    }

    private static DebtDetails Details(
        long amount,
        Currency currency = Currency.PLN,
        DateOnly? date = null,
        DateOnly? dueDate = null,
        Guid? accountId = null,
        long? accountAmount = null,
        decimal? rate = null) =>
        new(
            DebtDirection.Lent,
            "Max",
            amount,
            currency,
            date ?? Date,
            dueDate,
            accountId,
            accountAmount,
            rate,
            null);
}
