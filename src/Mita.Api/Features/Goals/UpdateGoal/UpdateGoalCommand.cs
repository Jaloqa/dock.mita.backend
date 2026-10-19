using System.Text.Json;
using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Accounts;
using Mita.Api.Common.Caching;
using Mita.Api.Common.Goals;

namespace Mita.Api.Features.Goals.UpdateGoal;

public sealed record UpdateGoalRequest(JsonElement? Name, JsonElement? Target, JsonElement? Deadline, JsonElement? AccountId);

public sealed record UpdateGoalCommand(Guid Id, JsonElement? Name, JsonElement? Target, JsonElement? Deadline, JsonElement? AccountId)
    : IRequest<Result<GoalDto>>, IGoalInput, IInvalidatesCache
{
    public Guid? ExistingId => Id;

    public IReadOnlyCollection<string> CacheTagsToInvalidate => [AccountCacheTags.Accounts];
}
