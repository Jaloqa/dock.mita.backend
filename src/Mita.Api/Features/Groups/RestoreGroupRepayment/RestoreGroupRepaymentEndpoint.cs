using MediatR;
using Microsoft.AspNetCore.Mvc;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.RestoreGroupRepayment;

public static class RestoreGroupRepaymentEndpoint
{
    public static RouteGroupBuilder MapRestoreGroupRepayment(this RouteGroupBuilder group)
    {
        group.MapPost(
                "/groups/{id:guid}/repayments/{repaymentId:guid}/restore",
                (Guid id, Guid repaymentId, IMediator mediator, CancellationToken ct) =>
                    RestoreAsync(GroupScope.Owner(id), repaymentId, mediator, ct))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }

    public static RouteGroupBuilder MapSharedRestoreGroupRepayment(this RouteGroupBuilder group)
    {
        group.MapPost(
            "/repayments/{repaymentId:guid}/restore",
            (
                string token,
                Guid repaymentId,
                [FromHeader(Name = GroupAccess.MemberHeader)] string? member,
                IMediator mediator,
                CancellationToken ct) => RestoreAsync(GroupScope.Link(token, member), repaymentId, mediator, ct));

        return group;
    }

    private static async Task<IResult> RestoreAsync(
        GroupScope scope,
        Guid repaymentId,
        IMediator mediator,
        CancellationToken ct) =>
        (await mediator.Send(new RestoreGroupRepaymentCommand(scope, repaymentId), ct))
            .ToHttp(repayment => Results.Ok(repayment));
}
