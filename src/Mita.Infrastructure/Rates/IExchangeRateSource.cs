using Mita.Domain.Money;

namespace Mita.Infrastructure.Rates;

public sealed record PlnRate(Currency Currency, DateOnly Date, decimal Value);

public interface IExchangeRateSource
{
    Task<IReadOnlyList<PlnRate>> FetchAsync(DateOnly from, DateOnly to, CancellationToken ct);
}
