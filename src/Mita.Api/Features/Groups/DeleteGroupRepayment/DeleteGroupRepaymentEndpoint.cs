using MediatR;
using Microsoft.AspNetCore.Mvc;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.DeleteGroupRepayment;

public static class DeleteGroupRepaymentEndpoint
{
    public static RouteGroupBuilder MapDeleteGroupRepayment(this RouteGroupBuilder group)
    {
        group.MapDelete(
                "/groups/{id:guid}/repayments/{repaymentId:guid}",
                (Guid id, Guid repaymentId, IMediator mediator, CancellationToken ct) =>
                    DeleteAsync(GroupScope.Owner(id), repaymentId, mediator, ct))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }

    public static RouteGroupBuilder MapSharedDeleteGroupRepayment(this RouteGroupBuilder group)
    {
        group.MapDelete(
            "/repayments/{repaymentId:guid}",
            (
                string token,
                Guid repaymentId,
                [FromHeader(Name = GroupAccess.MemberHeader)] string? member,
                IMediator mediator,
                CancellationToken ct) => DeleteAsync(GroupScope.Link(token, member), repaymentId, mediator, ct));

        return group;
    }

    private static async Task<IResult> DeleteAsync(
        GroupScope scope,
        Guid repaymentId,
        IMediator mediator,
        CancellationToken ct) =>
        (await mediator.Send(new DeleteGroupRepaymentCommand(scope, repaymentId), ct))
            .ToHttp(() => Results.NoContent());
}
