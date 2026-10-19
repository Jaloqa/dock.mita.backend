using Mita.Api.Extensions;
using Mita.Api.Features.Overview.GetOverview;

namespace Mita.Api.Features.Overview;

public static class OverviewEndpoints
{
    public static IEndpointRouteBuilder MapOverviewEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapV1()
            .WithTags("Overview")
            .MapGetOverview();

        return app;
    }
}
