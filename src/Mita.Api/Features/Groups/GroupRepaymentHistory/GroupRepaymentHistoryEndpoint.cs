using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.GroupRepaymentHistory;

public static class GroupRepaymentHistoryEndpoint
{
    public static RouteGroupBuilder MapGroupRepaymentHistory(this RouteGroupBuilder group)
    {
        group.MapGet(
                "/groups/{id:guid}/repayments/{repaymentId:guid}/history",
                async (Guid id, Guid repaymentId, IMediator mediator, CancellationToken ct, int page = 1, int pageSize = 100) =>
                    (await mediator.Send(new GroupRepaymentHistoryQuery(new GroupScope(id), repaymentId, page, pageSize), ct))
                        .ToHttp(history => Results.Ok(history)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
