using Mita.Api.Extensions;
using Mita.Api.Features.Accounts.CreateAccount;
using Mita.Api.Features.Accounts.DeleteAccount;
using Mita.Api.Features.Accounts.ListAccounts;
using Mita.Api.Features.Accounts.UpdateAccount;

namespace Mita.Api.Features.Accounts;

public static class AccountsEndpoints
{
    public static IEndpointRouteBuilder MapAccountsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapV1()
            .WithTags("Accounts")
            .MapListAccounts()
            .MapCreateAccount()
            .MapUpdateAccount()
            .MapDeleteAccount();

        return app;
    }
}
