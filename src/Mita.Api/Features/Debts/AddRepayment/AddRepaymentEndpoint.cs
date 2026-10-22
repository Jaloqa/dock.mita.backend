using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Debts.AddRepayment;

public static class AddRepaymentEndpoint
{
    public static RouteGroupBuilder MapAddRepayment(this RouteGroupBuilder group)
    {
        group.MapPost(
                "/debts/{id:guid}/repayments",
                async (Guid id, AddRepaymentRequest? request, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(
                        new AddRepaymentCommand(
                            id,
                            request?.Date,
                            request?.Amount,
                            request?.Currency,
                            request?.Rate,
                            request?.AccountId,
                            request?.Note),
                        ct))
                        .ToHttp(debt => Results.Ok(debt)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
