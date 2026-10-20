using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Caching;
using Mita.Api.Common.Rates;

namespace Mita.Api.Features.Rates.GetRates;

public sealed record GetRatesQuery(string Date, string? From, string? To, string Today)
    : IRequest<Result<IReadOnlyList<RateDto>>>, ICachedQuery
{
    public bool IsUserScoped => false;

    public bool IsCacheable => Date == Today;

    public string CacheKey => $"rates:{Date}:{From ?? "*"}:{To ?? "*"}";

    public IReadOnlyCollection<string> CacheTags => [RateCacheTags.Rates];

    public TimeSpan? CacheExpiration => TimeSpan.FromHours(1);
}
