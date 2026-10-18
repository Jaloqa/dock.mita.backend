using Mita.Api.Extensions;
using Mita.Api.Features.Accounts.CreateAccount;
using Mita.Api.Features.Accounts.ListAccounts;

namespace Mita.Api.Features.Accounts;

public static class AccountsEndpoints
{
    public static IEndpointRouteBuilder MapAccountsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapV1()
            .WithTags("Accounts")
            .MapListAccounts()
            .MapCreateAccount();

        return app;
    }
}
