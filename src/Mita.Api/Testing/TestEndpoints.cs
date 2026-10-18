using Mita.Api.Common;

namespace Mita.Api.Testing;

public static class TestEndpoints
{
    public static IEndpointRouteBuilder MapTestEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/test/throw",
            IResult () => throw new InvalidOperationException("Test failure")).AllowAnonymous();

        endpoints.MapPost("/test/echo", (EchoBody body) => Results.Ok(body)).AllowAnonymous();

        endpoints.MapGet(
            "/test/page",
            ([AsParameters] PageRequest request) =>
                Results.Ok(new PageResponse<int>([], request.Page, request.PageSize, 0))).AllowAnonymous();

        endpoints.MapTestCounterEndpoints();

        return endpoints;
    }
}
