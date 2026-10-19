using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common.Categories;
using Mita.Domain.Categories;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Categories.UpdateCategory;

public sealed class UpdateCategoryKindValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryKindValidator(AppDbContext db, ICurrentUser currentUser)
    {
        RuleFor(command => command.Kind)
            .MustAsync(async (command, _, cancellationToken) =>
            {
                if (!CategoryWire.TryParse(command.Kind, out CategoryKind requested))
                {
                    return true;
                }

                var userId = currentUser.Id;
                var used = db.UsedCategoryIds(userId);
                var stored = await db.Categories.AsNoTracking()
                    .Where(category => category.Id == command.Id && category.UserId == userId)
                    .Select(category => new
                    {
                        category.Kind,
                        Locked = category.System != null || used.Contains(category.Id)
                    })
                    .SingleOrDefaultAsync(cancellationToken);

                return stored is null || stored.Kind == requested || !stored.Locked;
            })
            .WithErrorCode("validation.categoryKindLocked")
            .When(command => CategoryWire.TryParse(command.Kind, out CategoryKind _));
    }
}
