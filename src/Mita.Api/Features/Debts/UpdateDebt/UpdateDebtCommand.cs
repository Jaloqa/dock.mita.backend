using System.Text.Json;
using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Accounts;
using Mita.Api.Common.Caching;
using Mita.Api.Common.Debts;

namespace Mita.Api.Features.Debts.UpdateDebt;

public sealed record UpdateDebtRequest(
    JsonElement? Direction,
    JsonElement? Counterparty,
    JsonElement? Amount,
    JsonElement? Currency,
    JsonElement? Date,
    JsonElement? DueDate,
    JsonElement? AccountId,
    JsonElement? Rate,
    JsonElement? Note);

public sealed record UpdateDebtCommand(
    Guid Id,
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
    public Guid? ExistingId => Id;

    public IReadOnlyCollection<string> CacheTagsToInvalidate => [AccountCacheTags.Accounts];
}
