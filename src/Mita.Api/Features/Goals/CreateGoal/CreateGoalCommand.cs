using System.Text.Json;
using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Accounts;
using Mita.Api.Common.Caching;
using Mita.Api.Common.Goals;

namespace Mita.Api.Features.Goals.CreateGoal;

public sealed record CreateGoalRequest(JsonElement? Name, JsonElement? Target, JsonElement? Deadline, JsonElement? AccountId);

public sealed record CreateGoalCommand(JsonElement? Name, JsonElement? Target, JsonElement? Deadline, JsonElement? AccountId)
    : IRequest<Result<GoalDto>>, IGoalInput, IInvalidatesCache
{
    public Guid? ExistingId => null;

    public IReadOnlyCollection<string> CacheTagsToInvalidate => [AccountCacheTags.Accounts];
}
