using Mita.Api.Extensions;
using Mita.Api.Features.Transfers.CreateTransfer;
using Mita.Api.Features.Transfers.ListTransfers;

namespace Mita.Api.Features.Transfers;

public static class TransfersEndpoints
{
    public static IEndpointRouteBuilder MapTransfersEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapV1()
            .WithTags("Transfers")
            .MapListTransfers()
            .MapCreateTransfer();

        return app;
    }
}
