using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Debts.DeleteRepayment;

public static class DeleteRepaymentEndpoint
{
    public static RouteGroupBuilder MapDeleteRepayment(this RouteGroupBuilder group)
    {
        group.MapDelete(
                "/debts/{id:guid}/repayments/{repaymentId:guid}",
                async (Guid id, Guid repaymentId, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new DeleteRepaymentCommand(id, repaymentId), ct)).ToHttp(() => Results.NoContent()))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
