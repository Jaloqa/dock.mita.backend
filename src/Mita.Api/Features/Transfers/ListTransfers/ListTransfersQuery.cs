using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Accounts;
using Mita.Api.Common.Caching;
using Mita.Api.Common.Transfers;

namespace Mita.Api.Features.Transfers.ListTransfers;

public sealed record ListTransfersQuery(string? AccountId, int Page, int PageSize)
    : IRequest<Result<PageResponse<TransferDto>>>, ICachedQuery
{
    public string CacheKey => $"transfers:{AccountId ?? "*"}:{Page}:{PageSize}";

    public IReadOnlyCollection<string> CacheTags => [AccountCacheTags.Transfers];

    public TimeSpan? CacheExpiration => null;
}
