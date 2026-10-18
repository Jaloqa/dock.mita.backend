using System.Text.Json;
using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Accounts;
using Mita.Api.Common.Caching;
using Mita.Api.Common.Transfers;

namespace Mita.Api.Features.Transfers.CreateTransfer;

public sealed record CreateTransferRequest(
    JsonElement? Date,
    JsonElement? FromAccountId,
    JsonElement? ToAccountId,
    JsonElement? FromAmount,
    JsonElement? Rate,
    JsonElement? Note);

public sealed record CreateTransferCommand(
    JsonElement? Date,
    JsonElement? FromAccountId,
    JsonElement? ToAccountId,
    JsonElement? FromAmount,
    JsonElement? Rate,
    JsonElement? Note) : IRequest<Result<TransferDto>>, IInvalidatesCache
{
    public IReadOnlyCollection<string> CacheTagsToInvalidate => [AccountCacheTags.Transfers, AccountCacheTags.Accounts];
}
