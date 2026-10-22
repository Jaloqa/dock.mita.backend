using Mita.Domain.Money;

namespace Mita.Infrastructure.Rates;

internal static class SyncedCurrencies
{
    public static IReadOnlyList<Currency> All { get; } = [Currency.EUR, Currency.USD, Currency.UAH];
}
