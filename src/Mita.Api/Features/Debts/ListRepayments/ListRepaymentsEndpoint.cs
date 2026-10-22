using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Debts.ListRepayments;

public static class ListRepaymentsEndpoint
{
    public static RouteGroupBuilder MapListRepayments(this RouteGroupBuilder group)
    {
        group.MapGet(
                "/debts/{id:guid}/repayments",
                async (Guid id, IMediator mediator, CancellationToken ct, int page = 1, int pageSize = 100) =>
                    (await mediator.Send(new ListRepaymentsQuery(id, page, pageSize), ct))
                        .ToHttp(repayments => Results.Ok(repayments)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
