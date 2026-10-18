using System.Text;
using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Caching;
using Mita.Api.Common.Users;

namespace Mita.Api.Features.Users.DeleteMe;

public sealed record DeleteMeRequest(string? Password);

public sealed record DeleteMeCommand(string? Password) : IRequest<Result>, IInvalidatesCache
{
    public IReadOnlyCollection<string> CacheTagsToInvalidate => [UserCacheTags.Me];

    public bool InvalidatesAllUserEntries => true;

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("HasPassword = ").Append(!string.IsNullOrEmpty(Password));
        return true;
    }
}
