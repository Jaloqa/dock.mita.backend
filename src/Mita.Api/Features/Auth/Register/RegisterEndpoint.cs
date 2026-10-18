using MediatR;
using Mita.Api.Authentication;
using Mita.Api.Common;

namespace Mita.Api.Features.Auth.Register;

public static class RegisterEndpoint
{
    public const string MePath = "/v1/me";

    public static RouteGroupBuilder MapRegister(this RouteGroupBuilder group)
    {
        group.MapPost(
                "/register",
                async (RegisterRequest? request, HttpContext http, IMediator mediator, IHostEnvironment env, CancellationToken ct) =>
                {
                    var command = new RegisterCommand(
                        request?.Name,
                        request?.Email,
                        request?.Password,
                        AcceptLanguageLocale.Resolve(http.Request.GetTypedHeaders().AcceptLanguage));

                    var result = await mediator.Send(command, ct);

                    return result.ToHttp(response =>
                    {
                        AuthCookies.Write(http.Response, response.Tokens, AuthCookies.NewXsrfToken(), !env.IsDevelopment());
                        return Results.Created(MePath, response.User);
                    });
                })
            .AllowAnonymous();

        return group;
    }
}
