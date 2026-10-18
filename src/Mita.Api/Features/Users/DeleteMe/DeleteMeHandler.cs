using MediatR;
using Microsoft.AspNetCore.Identity;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Infrastructure.Identity;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Users.DeleteMe;

public sealed class DeleteMeHandler(
    AppDbContext db,
    UserManager<AppUser> userManager,
    LockoutCounter lockoutCounter,
    ICurrentUser currentUser)
    : IRequestHandler<DeleteMeCommand, Result>
{
    public async Task<Result> Handle(DeleteMeCommand command, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(currentUser.Id.ToString());
        if (user is null)
        {
            return Error.NotFound();
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return InvalidPassword();
        }

        if (!await userManager.CheckPasswordAsync(user, command.Password!))
        {
            await lockoutCounter.RegisterFailureAsync(user.Id, cancellationToken);
            return InvalidPassword();
        }

        var deleted = await DeleteAsync(user);
        if (!deleted.Succeeded)
        {
            throw new InvalidOperationException(
                $"User deletion failed: {string.Join(", ", deleted.Errors.Select(error => error.Code))}");
        }

        return Result.Success();
    }

    // The password was already verified in this request, so a concurrent change of the account (for example a
    // lockout counter update) must not turn a confirmed deletion into an error.
    private async Task<IdentityResult> DeleteAsync(AppUser user)
    {
        var deleted = await userManager.DeleteAsync(user);
        if (deleted.Succeeded
            || !deleted.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure)))
        {
            return deleted;
        }

        db.ChangeTracker.Clear();
        var reloaded = await userManager.FindByIdAsync(user.Id.ToString());
        return reloaded is null ? IdentityResult.Success : await userManager.DeleteAsync(reloaded);
    }

    private static Error InvalidPassword() =>
        Error.Validation(new Dictionary<string, string[]> { ["password"] = ["validation.invalidPassword"] });
}
