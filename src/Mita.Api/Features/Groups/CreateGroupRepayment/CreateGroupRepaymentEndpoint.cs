using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.CreateGroupRepayment;

public static class CreateGroupRepaymentEndpoint
{
    public static RouteGroupBuilder MapCreateGroupRepayment(this RouteGroupBuilder group)
    {
        group.MapPost(
                "/groups/{id:guid}/repayments",
                async (Guid id, CreateGroupRepaymentRequest? request, IMediator mediator, CancellationToken ct) =>
                {
                    var command = new CreateGroupRepaymentCommand(
                        GroupScope.Owner(id),
                        request?.FromMemberId,
                        request?.ToMemberId,
                        request?.Amount,
                        request?.Currency,
                        request?.Date,
                        request?.Rate,
                        request?.Note);

                    return (await mediator.Send(command, ct))
                        .ToHttp(repayment => Results.Created((string?)null, repayment));
                })
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
