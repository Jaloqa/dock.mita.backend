using MediatR;
using Microsoft.AspNetCore.Mvc;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.LinkThroughShare;

public static class LinkThroughShareEndpoint
{
    public static RouteGroupBuilder MapLinkThroughShare(this RouteGroupBuilder group)
    {
        group.MapPut(
                "/membership",
                async (
                    string token,
                    [FromHeader(Name = GroupAccess.MemberHeader)] string? member,
                    IMediator mediator,
                    CancellationToken ct) =>
                    (await mediator.Send(new LinkThroughShareCommand(GroupScope.Link(token, member)), ct))
                        .ToHttp(summary => Results.Ok(summary)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
