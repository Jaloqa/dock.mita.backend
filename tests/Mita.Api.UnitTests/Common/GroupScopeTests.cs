using Mita.Api.Common.Groups;
using Mita.Api.Features.Groups.GetGroup;
using Shouldly;

namespace Mita.Api.UnitTests.Common;

public sealed class GroupScopeTests
{
    private const string Token = "Zm9v-YmFy_LWJhei1xdXV4LTAxMjM0NTY3ODlfYWJjZ";

    [Fact]
    public void Should_MaskToken_When_LinkScopePrinted()
    {
        const string member = "0f8fad5b-d9cb-469f-a165-70867728950e";

        GroupScope.Link(Token, member).ToString()
            .ShouldBe($"LinkGroupScope {{ Token = ***, MemberHeader = {member} }}");
    }

    [Fact]
    public void Should_MaskToken_When_RequestWithLinkScopePrinted()
    {
        var printed = new GetGroupQuery(GroupScope.Link(Token, null)).ToString();

        printed.ShouldNotContain(Token);
        printed.ShouldBe("GetGroupQuery { Scope = LinkGroupScope { Token = ***, MemberHeader =  } }");
    }

    [Fact]
    public void Should_CompareTokens_When_LinkScopesMasked()
    {
        GroupScope.Link(Token, null).ShouldBe(GroupScope.Link(Token, null));
        GroupScope.Link(Token, null).ShouldNotBe(GroupScope.Link(Token.Replace('Z', 'Y'), null));
    }
}
