using System.Text;
using Mita.Api.Common.Logging;

namespace Mita.Api.Common.Groups;

public abstract record GroupScope
{
    public static GroupScope User(Guid groupId) => new UserGroupScope(groupId);

    public static GroupScope Link(string token, string? memberHeader) => new LinkGroupScope(token, memberHeader);
}

public sealed record UserGroupScope(Guid GroupId) : GroupScope;

public sealed record LinkGroupScope(string Token, string? MemberHeader) : GroupScope
{
    protected override bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Token = ").Append(SensitiveDataMaskingEnricher.Mask)
            .Append(", MemberHeader = ").Append(MemberHeader);
        return true;
    }
}
