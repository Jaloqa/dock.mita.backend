using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Settings.UpdateSettings;

public static class UpdateSettingsEndpoint
{
    public static RouteGroupBuilder MapUpdateSettings(this RouteGroupBuilder group)
    {
        group.MapPut(
                "/settings",
                async (UpdateSettingsRequest? request, IMediator mediator, CancellationToken ct) =>
                {
                    var command = new UpdateSettingsCommand(
                        request?.BaseCurrency,
                        request?.Locale,
                        request?.MonthStartDay,
                        request?.Theme,
                        request?.Notifications);

                    return (await mediator.Send(command, ct)).ToHttp(settings => Results.Ok(settings));
                })
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}