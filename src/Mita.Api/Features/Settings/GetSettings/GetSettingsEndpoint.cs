using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Settings.GetSettings;

public static class GetSettingsEndpoint
{
    public static RouteGroupBuilder MapGetSettings(this RouteGroupBuilder group)
    {
        group.MapGet(
                "/settings",
                async (IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new GetSettingsQuery(), ct)).ToHttp(settings => Results.Ok(settings)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}