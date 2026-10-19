using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Accounts;
using Mita.Api.Common.Caching;

namespace Mita.Api.Features.Goals.DeleteGoal;

public sealed record DeleteGoalCommand(Guid Id) : IRequest<Result>, IInvalidatesCache
{
    public IReadOnlyCollection<string> CacheTagsToInvalidate => [AccountCacheTags.Accounts];
}
