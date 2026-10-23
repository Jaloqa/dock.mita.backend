using MediatR;
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
                async (Guid id, Guid repaymentId, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new RestoreGroupRepaymentCommand(new GroupScope(id), repaymentId), ct))
                        .ToHttp(repayment => Results.Ok(repayment)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
