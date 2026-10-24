using System.Text.Json;
using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Accounts;
using Mita.Api.Common.Caching;
using Mita.Api.Common.Categories;

namespace Mita.Api.Features.ToConfirm.ConfirmItem;

public sealed record ConfirmItemRequest(JsonElement? Version, JsonElement? AccountId, JsonElement? Rate, JsonElement? Lines);

public sealed record ConfirmItemCommand(
    string Kind,
    Guid EntryId,
    JsonElement? Version,
    JsonElement? AccountId,
    JsonElement? Rate,
    JsonElement? Lines) : IRequest<Result>, IInvalidatesCache
{
    public IReadOnlyCollection<string> CacheTagsToInvalidate => [AccountCacheTags.Accounts, CategoryCacheTags.Categories];
}
