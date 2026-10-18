using Mita.Domain.Accounts;
using Mita.Domain.Money;
using Shouldly;

namespace Mita.Api.UnitTests.Accounts;

public sealed class AccountTests
{
    [Fact]
    public void Should_DropCardLast4_When_AccountIsNotCard()
    {
        var cash = Account.Create(Guid.NewGuid(), "Wallet", AccountType.Cash, Currency.PLN, 0, "1234", false);
        var card = Account.Create(Guid.NewGuid(), "Visa", AccountType.Card, Currency.PLN, 0, "1234", false);

        cash.CardLast4.ShouldBeNull();
        card.CardLast4.ShouldBe("1234");
    }

    [Fact]
    public void Should_DropCardLast4_When_UpdatedToNonCard()
    {
        var account = Account.Create(Guid.NewGuid(), "Visa", AccountType.Card, Currency.PLN, 0, "1234", false);

        account.Update("Visa", AccountType.Savings, Currency.EUR, 500, "1234", true);

        account.CardLast4.ShouldBeNull();
        account.Type.ShouldBe(AccountType.Savings);
        account.Currency.ShouldBe(Currency.EUR);
        account.OpeningBalance.ShouldBe(500);
        account.Archived.ShouldBeTrue();
    }
}
