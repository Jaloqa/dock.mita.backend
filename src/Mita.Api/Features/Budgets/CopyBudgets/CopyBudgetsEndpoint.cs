using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Budgets.CopyBudgets;

public static class CopyBudgetsEndpoint
{
    public static RouteGroupBuilder MapCopyBudgets(this RouteGroupBuilder group)
    {
        group.MapPost(
                "/budgets/copy",
                async (CopyBudgetsRequest? request, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new CopyBudgetsCommand(request?.Period), ct)).ToHttp(response => Results.Ok(response)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
