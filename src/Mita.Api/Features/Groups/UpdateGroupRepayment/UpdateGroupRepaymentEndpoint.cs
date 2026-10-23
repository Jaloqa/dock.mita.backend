using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.UpdateGroupRepayment;

public static class UpdateGroupRepaymentEndpoint
{
    public static RouteGroupBuilder MapUpdateGroupRepayment(this RouteGroupBuilder group)
    {
        group.MapPut(
                "/groups/{id:guid}/repayments/{repaymentId:guid}",
                async (Guid id, Guid repaymentId, UpdateGroupRepaymentRequest? request, IMediator mediator, CancellationToken ct) =>
                {
                    var command = new UpdateGroupRepaymentCommand(
                        GroupScope.Owner(id),
                        repaymentId,
                        request?.FromMemberId,
                        request?.ToMemberId,
                        request?.Amount,
                        request?.Currency,
                        request?.Date,
                        request?.Rate,
                        request?.Note);

                    return (await mediator.Send(command, ct)).ToHttp(repayment => Results.Ok(repayment));
                })
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
