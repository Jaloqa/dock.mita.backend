using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Caching;
using Mita.Api.Common.Users;

namespace Mita.Api.Features.Users.GetMe;

public sealed record GetMeQuery : IRequest<Result<UserDto>>, ICachedQuery
{
    public string CacheKey => UserCacheTags.Me;

    public IReadOnlyCollection<string> CacheTags => [UserCacheTags.Me];

    public TimeSpan? CacheExpiration => null;
}
