using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Money;
using Mita.Api.Common.Rates;
using Mita.Api.Common.Wire;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Rates.GetRates;

public sealed class GetRatesHandler(AppDbContext db)
    : IRequestHandler<GetRatesQuery, Result<IReadOnlyList<RateDto>>>
{
    private static readonly Currency[] AllCurrencies = Enum.GetValues<Currency>();

    public async Task<Result<IReadOnlyList<RateDto>>> Handle(GetRatesQuery query, CancellationToken cancellationToken)
    {
        if (!IsoDate.TryParse(query.Date, out var date))
        {
            throw new InvalidOperationException("The rates date must be validated before the handler runs.");
        }

        var sources = CurrencyWire.TryParse(query.From, out var from) ? [from] : AllCurrencies;
        var targets = CurrencyWire.TryParse(query.To, out var to) ? [to] : AllCurrencies;

        var plnValues = await db.PlnValuesAsync(date, cancellationToken);
        var wireDate = IsoDate.ToWire(date);
        var rates = new List<RateDto>();
        foreach (var source in sources)
        {
            foreach (var target in targets)
            {
                if (source != target && RateTable.Rate(plnValues, source, target) is { } value)
                {
                    rates.Add(new RateDto(wireDate, CurrencyWire.ToWire(source), CurrencyWire.ToWire(target), (double)value));
                }
            }
        }

        return rates;
    }
}
