using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Categories.DeleteCategory;

public sealed class DeleteCategoryValidator : AbstractValidator<DeleteCategoryCommand>
{
    public DeleteCategoryValidator(AppDbContext db, ICurrentUser currentUser)
    {
        RuleFor(command => command.Id)
            .MustAsync(async (id, cancellationToken) =>
            {
                var userId = currentUser.Id;
                return !await db.Categories.AnyAsync(
                    category => category.Id == id && category.UserId == userId && category.System != null,
                    cancellationToken);
            })
            .WithErrorCode("validation.systemCategory");
    }
}
