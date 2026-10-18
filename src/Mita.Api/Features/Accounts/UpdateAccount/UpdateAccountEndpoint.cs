using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Accounts.UpdateAccount;

public static class UpdateAccountEndpoint
{
    public static RouteGroupBuilder MapUpdateAccount(this RouteGroupBuilder group)
    {
        group.MapPut(
                "/accounts/{id:guid}",
                async (Guid id, UpdateAccountRequest? request, IMediator mediator, CancellationToken ct) =>
                {
                    var command = new UpdateAccountCommand(
                        id,
                        request?.Name,
                        request?.Type,
                        request?.Currency,
                        request?.OpeningBalance,
                        request?.CardLast4,
                        request?.Archived);

                    return (await mediator.Send(command, ct)).ToHttp(account => Results.Ok(account));
                })
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
