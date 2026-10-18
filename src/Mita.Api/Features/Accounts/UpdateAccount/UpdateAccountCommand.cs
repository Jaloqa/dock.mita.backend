using System.Text.Json;
using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Accounts;
using Mita.Api.Common.Caching;

namespace Mita.Api.Features.Accounts.UpdateAccount;

public sealed record UpdateAccountRequest(
    JsonElement? Name,
    JsonElement? Type,
    JsonElement? Currency,
    JsonElement? OpeningBalance,
    JsonElement? CardLast4,
    JsonElement? Archived);

public sealed record UpdateAccountCommand(
    Guid Id,
    JsonElement? Name,
    JsonElement? Type,
    JsonElement? Currency,
    JsonElement? OpeningBalance,
    JsonElement? CardLast4,
    JsonElement? Archived) : IRequest<Result<AccountDto>>, IAccountInput, IInvalidatesCache
{
    public IReadOnlyCollection<string> CacheTagsToInvalidate => [AccountCacheTags.Accounts];
}
