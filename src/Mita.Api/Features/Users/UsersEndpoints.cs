using Mita.Api.Extensions;
using Mita.Api.Features.Users.DeleteMe;
using Mita.Api.Features.Users.GetMe;
using Mita.Api.Features.Users.UpdateMe;

namespace Mita.Api.Features.Users;

public static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapV1()
            .WithTags("Users")
            .MapGetMe()
            .MapUpdateMe()
            .MapDeleteMe();

        return app;
    }
}
