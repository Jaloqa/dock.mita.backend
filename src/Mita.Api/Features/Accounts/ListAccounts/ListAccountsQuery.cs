using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Accounts;
using Mita.Api.Common.Caching;

namespace Mita.Api.Features.Accounts.ListAccounts;

public sealed record ListAccountsQuery(int Page, int PageSize)
    : IRequest<Result<PageResponse<AccountDto>>>, ICachedQuery
{
    public string CacheKey => $"accounts:{Page}:{PageSize}";

    public IReadOnlyCollection<string> CacheTags => [AccountCacheTags.Accounts];

    public TimeSpan? CacheExpiration => null;
}
