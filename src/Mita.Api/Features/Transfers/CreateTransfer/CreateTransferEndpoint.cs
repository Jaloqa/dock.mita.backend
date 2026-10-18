using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Transfers.CreateTransfer;

public static class CreateTransferEndpoint
{
    public static RouteGroupBuilder MapCreateTransfer(this RouteGroupBuilder group)
    {
        group.MapPost(
                "/transfers",
                async (CreateTransferRequest? request, IMediator mediator, CancellationToken ct) =>
                {
                    var command = new CreateTransferCommand(
                        request?.Date,
                        request?.FromAccountId,
                        request?.ToAccountId,
                        request?.FromAmount,
                        request?.Rate,
                        request?.Note);

                    return (await mediator.Send(command, ct))
                        .ToHttp(transfer => Results.Created((string?)null, transfer));
                })
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
