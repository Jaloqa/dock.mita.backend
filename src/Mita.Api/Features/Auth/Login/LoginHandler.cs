using MediatR;
using Microsoft.AspNetCore.Identity;
using Mita.Api.Common;
using Mita.Api.Common.Users;
using Mita.Infrastructure.Authentication;
using Mita.Infrastructure.Identity;

namespace Mita.Api.Features.Auth.Login;

public sealed class LoginHandler(
    UserManager<AppUser> userManager,
    IPasswordHasher<AppUser> passwordHasher,
    LockoutCounter lockoutCounter,
    ITokenService tokens)
    : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private static readonly Lazy<string> DummyHash = new(() =>
        new PasswordHasher<AppUser>().HashPassword(new AppUser(), Guid.NewGuid().ToString("N")));

    public async Task<Result<LoginResponse>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var password = command.Password!;
        var user = await userManager.FindByEmailAsync(command.Email!.Trim());

        if (user is null || await userManager.IsLockedOutAsync(user))
        {
            // Hashing anyway keeps the response time independent of whether the account exists or is locked.
            passwordHasher.VerifyHashedPassword(new AppUser(), DummyHash.Value, password);
            return InvalidCredentials();
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await lockoutCounter.RegisterFailureAsync(user.Id, cancellationToken);
            return InvalidCredentials();
        }

        if (user.AccessFailedCount > 0)
        {
            await lockoutCounter.ResetAsync(user.Id, cancellationToken);
        }

        var issued = await tokens.IssueAsync(user, null, cancellationToken);
        return new LoginResponse(UserDto.From(user), issued);
    }

    private static Error InvalidCredentials() => Error.Unauthorized("auth.invalidCredentials");
}
