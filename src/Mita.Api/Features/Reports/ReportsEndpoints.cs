using Mita.Api.Extensions;
using Mita.Api.Features.Reports.GetReport;

namespace Mita.Api.Features.Reports;

public static class ReportsEndpoints
{
    public static IEndpointRouteBuilder MapReportsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapV1()
            .WithTags("Reports")
            .MapGetReport();

        return app;
    }
}
