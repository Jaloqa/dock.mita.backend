using System.Text.Json;
using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Accounts;
using Mita.Api.Common.Caching;
using Mita.Api.Common.Categories;
using Mita.Api.Common.Transactions;

namespace Mita.Api.Features.Transactions.UpdateTransaction;

public sealed record UpdateTransactionRequest(
    JsonElement? Kind,
    JsonElement? Date,
    JsonElement? Title,
    JsonElement? Note,
    JsonElement? CategoryId,
    JsonElement? AccountId,
    JsonElement? Amount,
    JsonElement? Currency,
    JsonElement? Rate,
    JsonElement? Tags);

public sealed record UpdateTransactionCommand(
    Guid Id,
    JsonElement? Kind,
    JsonElement? Date,
    JsonElement? Title,
    JsonElement? Note,
    JsonElement? CategoryId,
    JsonElement? AccountId,
    JsonElement? Amount,
    JsonElement? Currency,
    JsonElement? Rate,
    JsonElement? Tags) : IRequest<Result<TransactionDto>>, ITransactionInput, IInvalidatesCache
{
    public Guid? ExistingId => Id;

    public IReadOnlyCollection<string> CacheTagsToInvalidate => [AccountCacheTags.Accounts, CategoryCacheTags.Categories];
}
