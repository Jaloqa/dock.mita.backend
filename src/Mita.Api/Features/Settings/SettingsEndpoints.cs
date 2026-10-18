using Mita.Api.Extensions;
using Mita.Api.Features.Settings.GetSettings;
using Mita.Api.Features.Settings.UpdateSettings;

namespace Mita.Api.Features.Settings;

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapV1()
            .WithTags("Settings")
            .MapGetSettings()
            .MapUpdateSettings();

        return app;
    }
}