using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Accounts.DeleteAccount;

public static class DeleteAccountEndpoint
{
    public static RouteGroupBuilder MapDeleteAccount(this RouteGroupBuilder group)
    {
        group.MapDelete(
                "/accounts/{id:guid}",
                async (Guid id, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new DeleteAccountCommand(id), ct)).ToHttp(() => Results.NoContent()))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
