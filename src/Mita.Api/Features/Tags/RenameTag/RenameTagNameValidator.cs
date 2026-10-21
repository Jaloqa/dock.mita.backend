using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common.Tags;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Tags.RenameTag;

public sealed class RenameTagNameValidator : AbstractValidator<RenameTagCommand>
{
    public RenameTagNameValidator(AppDbContext db, ICurrentUser currentUser) =>
        RuleFor(command => command.Name)
            .MustAsync(async (command, _, cancellationToken) =>
            {
                if (!TagNames.TryParseName(command.Name, out var name))
                {
                    return true;
                }

                var userId = currentUser.Id;

                return !await db.Tags
                    .FromSql($"""SELECT * FROM tags WHERE "UserId" = {userId} AND lower("Name") = lower({name})""")
                    .AnyAsync(tag => tag.Id != command.Id, cancellationToken);
            })
            .WithErrorCode("validation.tagNameTaken")
            .When(command => TagNames.NameError(command.Name) is null);
}
