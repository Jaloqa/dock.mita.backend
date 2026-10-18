using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Common;
using Mita.Api.Common.Users;
using Mita.Domain.Users;
using Mita.Infrastructure.Authentication;
using Mita.Infrastructure.Identity;
using Mita.Infrastructure.Persistence;
using Npgsql;

namespace Mita.Api.Features.Auth.Register;

public sealed class RegisterHandler(AppDbContext db, UserManager<AppUser> userManager, ITokenService tokens)
    : IRequestHandler<RegisterCommand, Result<RegisterResponse>>
{
    private static readonly string[] DuplicateErrorCodes =
    [
        nameof(IdentityErrorDescriber.DuplicateEmail),
        nameof(IdentityErrorDescriber.DuplicateUserName)
    ];

    public async Task<Result<RegisterResponse>> Handle(RegisterCommand command, CancellationToken cancellationToken)
    {
        var email = command.Email!.Trim();
        var user = new AppUser
        {
            Id = Guid.CreateVersion7(),
            Name = command.Name!.Trim(),
            Email = email,
            UserName = email
        };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var created = await userManager.CreateAsync(user, command.Password!);
            if (!created.Succeeded)
            {
                return created.Errors.Any(error => DuplicateErrorCodes.Contains(error.Code))
                    ? EmailTaken()
                    : throw new InvalidOperationException(
                        $"User creation failed: {string.Join(", ", created.Errors.Select(error => error.Code))}");
            }

            db.UserSettings.Add(UserSettings.CreateDefault(user.Id, command.Locale));
            await db.SaveChangesAsync(cancellationToken);

            var issued = await tokens.IssueAsync(user, null, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new RegisterResponse(UserDto.From(user), issued);
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
