using MediatR;
using Microsoft.AspNetCore.Mvc;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.RestoreExpense;

public static class RestoreExpenseEndpoint
{
    public static RouteGroupBuilder MapRestoreExpense(this RouteGroupBuilder group)
    {
        group.MapPost(
                "/groups/{id:guid}/expenses/{expenseId:guid}/restore",
                (Guid id, Guid expenseId, IMediator mediator, CancellationToken ct) =>
                    RestoreAsync(GroupScope.Owner(id), expenseId, mediator, ct))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }

    public static RouteGroupBuilder MapSharedRestoreExpense(this RouteGroupBuilder group)
    {
        group.MapPost(
            "/expenses/{expenseId:guid}/restore",
            (
                string token,
                Guid expenseId,
                [FromHeader(Name = GroupAccess.MemberHeader)] string? member,
                IMediator mediator,
                CancellationToken ct) => RestoreAsync(GroupScope.Link(token, member), expenseId, mediator, ct));

        return group;
    }

    private static async Task<IResult> RestoreAsync(
        GroupScope scope,
        Guid expenseId,
        IMediator mediator,
        CancellationToken ct) =>
        (await mediator.Send(new RestoreExpenseCommand(scope, expenseId), ct)).ToHttp(expense => Results.Ok(expense));
}
