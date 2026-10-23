using MediatR;
using Microsoft.AspNetCore.Mvc;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.UpdateExpense;

public static class UpdateExpenseEndpoint
{
    public static RouteGroupBuilder MapUpdateExpense(this RouteGroupBuilder group)
    {
        group.MapPut(
                "/groups/{id:guid}/expenses/{expenseId:guid}",
                (Guid id, Guid expenseId, UpdateExpenseRequest? request, IMediator mediator, CancellationToken ct) =>
                    UpdateAsync(GroupScope.Owner(id), expenseId, request, mediator, ct))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }

    public static RouteGroupBuilder MapSharedUpdateExpense(this RouteGroupBuilder group)
    {
        group.MapPut(
            "/expenses/{expenseId:guid}",
            (
                string token,
                Guid expenseId,
                [FromHeader(Name = GroupAccess.MemberHeader)] string? member,
                UpdateExpenseRequest? request,
                IMediator mediator,
                CancellationToken ct) => UpdateAsync(GroupScope.Link(token, member), expenseId, request, mediator, ct));

        return group;
    }

    private static async Task<IResult> UpdateAsync(
        GroupScope scope,
        Guid expenseId,
        UpdateExpenseRequest? request,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new UpdateExpenseCommand(
            scope,
            expenseId,
            request?.PayerId,
            request?.Amount,
            request?.Currency,
            request?.Date,
            request?.CategoryId,
            request?.Tags,
            request?.Description,
            request?.Rate,
            request?.Split);

        return (await mediator.Send(command, ct)).ToHttp(expense => Results.Ok(expense));
    }
}
