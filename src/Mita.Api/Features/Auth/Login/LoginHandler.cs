using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Mita.Api.Common;
using Mita.Api.Common.Users;
using Mita.Infrastructure.Authentication;
using Mita.Infrastructure.Identity;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Auth.Login;

public sealed class LoginHandler(
    AppDbContext db,
    UserManager<AppUser> userManager,
    IPasswordHasher<AppUser> passwordHasher,
    IOptions<IdentityOptions> identityOptions,
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
            await RecordFailedAttemptAsync(user.Id, cancellationToken);
            return InvalidCredentials();
        }

        if (user.AccessFailedCount > 0)
        {
            await ResetFailedAttemptsAsync(user.Id, cancellationToken);
        }

        var issued = await tokens.IssueAsync(user, null, cancellationToken);
        return new LoginResponse(UserDto.From(user), issued);
    }

    // UserManager.AccessFailedAsync is a read-modify-write guarded by the concurrency stamp, so parallel failures
    // overwrite each other and never reach the lockout threshold. A single UPDATE lets the database serialise them.
    private Task<int> RecordFailedAttemptAsync(Guid userId, CancellationToken cancellationToken)
    {
        var lockout = identityOptions.Value.Lockout;
        var threshold = lockout.MaxFailedAccessAttempts;
        var lockoutEnd = DateTimeOffset.UtcNow + lockout.DefaultLockoutTimeSpan;
        var stamp = Guid.NewGuid().ToString();

        return db.Users
            .Where(row => row.Id == userId)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(
                        row => row.LockoutEnd,
                        row => row.LockoutEnabled && row.AccessFailedCount + 1 >= threshold ? lockoutEnd : row.LockoutEnd)
                    .SetProperty(
                        row => row.AccessFailedCount,
                        row => row.LockoutEnabled && row.AccessFailedCount + 1 >= threshold ? 0 : row.AccessFailedCount + 1)
                    .SetProperty(row => row.ConcurrencyStamp, stamp),
                cancellationToken);
    }

    private Task<int> ResetFailedAttemptsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var stamp = Guid.NewGuid().ToString();

        return db.Users
            .Where(row => row.Id == userId && row.AccessFailedCount > 0)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(row => row.AccessFailedCount, 0)
                    .SetProperty(row => row.ConcurrencyStamp, stamp),
                cancellationToken);
    }

    private static Error InvalidCredentials() => Error.Unauthorized("auth.invalidCredentials");
}
