using System.Text.Json;
using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Accounts;
using Mita.Api.Common.Caching;
using Mita.Api.Common.Debts;

namespace Mita.Api.Features.Debts.CreateDebt;

public sealed record CreateDebtRequest(
    JsonElement? Direction,
    JsonElement? Counterparty,
    JsonElement? Amount,
    JsonElement? Currency,
    JsonElement? Date,
    JsonElement? DueDate,
    JsonElement? AccountId,
    JsonElement? Rate,
    JsonElement? Note);

public sealed record CreateDebtCommand(
    JsonElement? Direction,
    JsonElement? Counterparty,
    JsonElement? Amount,
    JsonElement? Currency,
    JsonElement? Date,
    JsonElement? DueDate,
    JsonElement? AccountId,
    JsonElement? Rate,
    JsonElement? Note) : IRequest<Result<DebtDto>>, IDebtInput, IInvalidatesCache
{
    public Guid? ExistingId => null;

    public IReadOnlyCollection<string> CacheTagsToInvalidate => [AccountCacheTags.Accounts];
}
