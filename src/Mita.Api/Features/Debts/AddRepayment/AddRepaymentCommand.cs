using System.Text.Json;
using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Accounts;
using Mita.Api.Common.Caching;
using Mita.Api.Common.Debts;

namespace Mita.Api.Features.Debts.AddRepayment;

public sealed record AddRepaymentRequest(
    JsonElement? Date,
    JsonElement? Amount,
    JsonElement? Currency,
    JsonElement? Rate,
    JsonElement? AccountId,
    JsonElement? Note);

public sealed record AddRepaymentCommand(
    Guid DebtId,
    JsonElement? Date,
    JsonElement? Amount,
    JsonElement? Currency,
    JsonElement? Rate,
    JsonElement? AccountId,
    JsonElement? Note) : IRequest<Result<DebtDto>>, IInvalidatesCache
{
    public IReadOnlyCollection<string> CacheTagsToInvalidate => [AccountCacheTags.Accounts];
}
