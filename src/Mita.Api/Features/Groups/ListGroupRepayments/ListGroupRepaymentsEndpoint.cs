using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.ListGroupRepayments;

public static class ListGroupRepaymentsEndpoint
{
    public static RouteGroupBuilder MapListGroupRepayments(this RouteGroupBuilder group)
    {
        group.MapGet(
                "/groups/{id:guid}/repayments",
                async (Guid id, IMediator mediator, CancellationToken ct, string? deleted = null, int page = 1, int pageSize = 100) =>
                    (await mediator.Send(new ListGroupRepaymentsQuery(new GroupScope(id), deleted, page, pageSize), ct))
                        .ToHttp(repayments => Results.Ok(repayments)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
