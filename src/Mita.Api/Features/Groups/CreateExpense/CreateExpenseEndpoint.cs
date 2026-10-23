using MediatR;
using Microsoft.AspNetCore.Mvc;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.CreateExpense;

public static class CreateExpenseEndpoint
{
    public static RouteGroupBuilder MapCreateExpense(this RouteGroupBuilder group)
    {
        group.MapPost(
                "/groups/{id:guid}/expenses",
                (Guid id, CreateExpenseRequest? request, IMediator mediator, CancellationToken ct) =>
                    CreateAsync(GroupScope.Owner(id), request, mediator, ct))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }

    public static RouteGroupBuilder MapSharedCreateExpense(this RouteGroupBuilder group)
    {
        group.MapPost(
            "/expenses",
            (
                string token,
                [FromHeader(Name = GroupAccess.MemberHeader)] string? member,
                CreateExpenseRequest? request,
                IMediator mediator,
                CancellationToken ct) => CreateAsync(GroupScope.Link(token, member), request, mediator, ct));

        return group;
    }

    private static async Task<IResult> CreateAsync(
        GroupScope scope,
        CreateExpenseRequest? request,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new CreateExpenseCommand(
            scope,
            request?.PayerId,
            request?.Amount,
            request?.Currency,
            request?.Date,
            request?.CategoryId,
            request?.Tags,
            request?.Description,
            request?.Rate,
            request?.Split);

        return (await mediator.Send(command, ct)).ToHttp(expense => Results.Created((string?)null, expense));
    }
}
