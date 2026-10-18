using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Users;
using Mita.Infrastructure.Authentication;
using Mita.Infrastructure.Identity;
using Mita.Infrastructure.Persistence;
using Npgsql;

namespace Mita.Api.Features.Users.UpdateMe;

public sealed class UpdateMeHandler(
    AppDbContext db,
    UserManager<AppUser> userManager,
    ITokenService tokens,
    SecurityStampCache stamps,
    ICurrentUser currentUser)
    : IRequestHandler<UpdateMeCommand, Result<UpdateMeResponse>>
{
    private const int MaxAttempts = 2;

    private static readonly string[] DuplicateErrorCodes =
    [
        nameof(IdentityErrorDescriber.DuplicateEmail),
        nameof(IdentityErrorDescriber.DuplicateUserName)
    ];

    public async Task<Result<UpdateMeResponse>> Handle(UpdateMeCommand command, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var result = await TryUpdateAsync(command, cancellationToken);
            if (result is not null)
            {
                return result;
            }

            db.ChangeTracker.Clear();
        }

        return Error.Conflict("user.concurrentUpdate");
    }

    private async Task<Result<UpdateMeResponse>?> TryUpdateAsync(UpdateMeCommand command, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(currentUser.Id.ToString());
        if (user is null)
        {
            return Error.NotFound();
        }

        var email = command.Email!.Trim();
        var emailChanged = !string.Equals(userManager.NormalizeEmail(email), user.NormalizedEmail, StringComparison.Ordinal);
        user.Name = command.Name!.Trim();
        user.UserName = email;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            IdentityResult updated;
            if (emailChanged)
            {
                updated = await userManager.SetEmailAsync(user, email);
            }
            else
            {
                user.Email = email;
                updated = await userManager.UpdateAsync(user);
            }

            if (!updated.Succeeded)
            {
                if (updated.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure)))
                {
                    return null;
                }

                return updated.Errors.Any(error => DuplicateErrorCodes.Contains(error.Code))
                    ? EmailTaken()
                    : throw new InvalidOperationException(
                        $"User update failed: {string.Join(", ", updated.Errors.Select(error => error.Code))}");
            }

            IssuedTokens? issued = null;
            if (emailChanged)
            {
                await tokens.RevokeAllAsync(user.Id, cancellationToken);
                issued = await tokens.IssueAsync(user, null, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            if (emailChanged)
            {
                await stamps.InvalidateAsync(user.Id);
            }

            return new UpdateMeResponse(UserDto.From(user), issued);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        })
        {
            return EmailTaken();
        }
    }

    private static Error EmailTaken() =>
        Error.Validation(new Dictionary<string, string[]> { ["email"] = ["validation.emailTaken"] });
}
