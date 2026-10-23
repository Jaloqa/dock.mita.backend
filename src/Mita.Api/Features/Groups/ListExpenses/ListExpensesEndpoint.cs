using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.ListExpenses;

public static class ListExpensesEndpoint
{
    public static RouteGroupBuilder MapListExpenses(this RouteGroupBuilder group)
    {
        group.MapGet(
                "/groups/{id:guid}/expenses",
                async (Guid id, IMediator mediator, CancellationToken ct, string? deleted = null, int page = 1, int pageSize = 100) =>
                    (await mediator.Send(new ListExpensesQuery(GroupScope.Owner(id), deleted, page, pageSize), ct))
                        .ToHttp(expenses => Results.Ok(expenses)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }

    public static RouteGroupBuilder MapSharedListExpenses(this RouteGroupBuilder group)
    {
        group.MapGet(
            "/expenses",
            async (string token, IMediator mediator, CancellationToken ct, string? deleted = null, int page = 1, int pageSize = 100) =>
                (await mediator.Send(new ListExpensesQuery(GroupScope.Link(token, null), deleted, page, pageSize), ct))
                    .ToHttp(expenses => Results.Ok(expenses)));

        return group;
    }
}
