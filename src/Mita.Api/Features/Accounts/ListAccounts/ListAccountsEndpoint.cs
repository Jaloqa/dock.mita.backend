using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Accounts.ListAccounts;

public static class ListAccountsEndpoint
{
    public static RouteGroupBuilder MapListAccounts(this RouteGroupBuilder group)
    {
        group.MapGet(
                "/accounts",
                async (IMediator mediator, CancellationToken ct, int page = 1, int pageSize = 100) =>
                    (await mediator.Send(new ListAccountsQuery(page, pageSize), ct)).ToHttp(accounts => Results.Ok(accounts)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
