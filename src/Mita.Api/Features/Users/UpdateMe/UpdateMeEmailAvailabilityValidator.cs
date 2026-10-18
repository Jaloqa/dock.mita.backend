using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common.Users;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Users.UpdateMe;

public sealed class UpdateMeEmailAvailabilityValidator : AbstractValidator<UpdateMeCommand>
{
    public UpdateMeEmailAvailabilityValidator(AppDbContext db, ILookupNormalizer normalizer, ICurrentUser currentUser)
    {
        RuleFor(command => command.Email)
            .MustAsync(async (email, ct) =>
                {
                    var userId = currentUser.Id;
                    var normalized = normalizer.NormalizeEmail(email!.Trim());
                    return !await db.Users.AnyAsync(user => user.NormalizedEmail == normalized && user.Id != userId, ct);
                })
            .WithErrorCode("validation.emailTaken")
            .When(command => !string.IsNullOrWhiteSpace(command.Email) && EmailAddress.IsValid(command.Email.Trim()));
    }
}
