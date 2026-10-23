using MediatR;
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
                async (Guid id, Guid repaymentId, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new DeleteGroupRepaymentCommand(new GroupScope(id), repaymentId), ct))
                        .ToHttp(() => Results.NoContent()))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
