using System.Text;
using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Caching;
using Mita.Api.Common.Users;

namespace Mita.Api.Features.Users.UpdateMe;

public sealed record UpdateMeRequest(string? Name, string? Email);

public sealed record UpdateMeCommand(string? Name, string? Email) : IRequest<Result<UpdateMeResponse>>, IInvalidatesCache
{
    public IReadOnlyCollection<string> CacheTagsToInvalidate => [UserCacheTags.Me];

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Email = ").Append(Email);
        return true;
    }
}
