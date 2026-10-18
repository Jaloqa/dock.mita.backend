using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Accounts.CreateAccount;

public static class CreateAccountEndpoint
{
    public static RouteGroupBuilder MapCreateAccount(this RouteGroupBuilder group)
    {
        group.MapPost(
                "/accounts",
                async (CreateAccountRequest? request, IMediator mediator, CancellationToken ct) =>
                {
                    var command = new CreateAccountCommand(
                        request?.Name,
                        request?.Type,
                        request?.Currency,
                        request?.OpeningBalance,
                        request?.CardLast4,
                        request?.Archived);

                    return (await mediator.Send(command, ct))
                        .ToHttp(account => Results.Created($"/v1/accounts/{account.Id}", account));
                })
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
