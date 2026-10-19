using System.Text.Json;
using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Accounts;
using Mita.Api.Common.Caching;
using Mita.Api.Common.Goals;

namespace Mita.Api.Features.Goals.ContributeToGoal;

public sealed record ContributeToGoalRequest(JsonElement? Amount, JsonElement? Date, JsonElement? FromAccountId, JsonElement? Rate);

public sealed record ContributeToGoalCommand(
    Guid Id,
    JsonElement? Amount,
    JsonElement? Date,
    JsonElement? FromAccountId,
    JsonElement? Rate) : IRequest<Result<GoalDto>>, IInvalidatesCache
{
    public IReadOnlyCollection<string> CacheTagsToInvalidate => [AccountCacheTags.Accounts, AccountCacheTags.Transfers];
}
