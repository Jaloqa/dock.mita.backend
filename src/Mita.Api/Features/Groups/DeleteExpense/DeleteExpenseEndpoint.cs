using MediatR;
using Microsoft.AspNetCore.Mvc;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.DeleteExpense;

public static class DeleteExpenseEndpoint
{
    public static RouteGroupBuilder MapDeleteExpense(this RouteGroupBuilder group)
    {
        group.MapDelete(
                "/groups/{id:guid}/expenses/{expenseId:guid}",
                (Guid id, Guid expenseId, IMediator mediator, CancellationToken ct) =>
                    DeleteAsync(GroupScope.User(id), expenseId, mediator, ct))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }

    public static RouteGroupBuilder MapSharedDeleteExpense(this RouteGroupBuilder group)
    {
        group.MapDelete(
            "/expenses/{expenseId:guid}",
            (
                string token,
                Guid expenseId,
                [FromHeader(Name = GroupAccess.MemberHeader)] string? member,
                IMediator mediator,
                CancellationToken ct) => DeleteAsync(GroupScope.Link(token, member), expenseId, mediator, ct));

        return group;
    }

    private static async Task<IResult> DeleteAsync(
        GroupScope scope,
        Guid expenseId,
        IMediator mediator,
        CancellationToken ct) =>
        (await mediator.Send(new DeleteExpenseCommand(scope, expenseId), ct)).ToHttp(() => Results.NoContent());
}
