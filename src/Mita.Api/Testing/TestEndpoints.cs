using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Extensions;

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

        endpoints.MapV1().MapMethods(
            "/test/echo-auth",
            [HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete],
            (ICurrentUser currentUser) => Results.Ok(new { userId = currentUser.Id }))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        endpoints.MapV1().MapPost(
            "/test/echo-body-auth",
            (EchoBody body) => Results.Ok(body))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        endpoints.MapTestCounterEndpoints();

        return endpoints;
    }
}
