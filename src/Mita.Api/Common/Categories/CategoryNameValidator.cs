using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Domain.Categories;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Categories;

public abstract class CategoryNameValidator<T> : AbstractValidator<T>
    where T : ICategoryInput
{
    protected CategoryNameValidator(AppDbContext db, ICurrentUser currentUser)
    {
        RuleFor(input => input.Name)
            .MustAsync(async (input, _, cancellationToken) =>
            {
                if (!CategoryInputParser.TryParseName(input.Name, out var name)
                    || !CategoryWire.TryParse(input.Kind, out CategoryKind kind))
                {
                    return true;
                }

                var userId = currentUser.Id;
                var storedKind = kind.ToString();
                var existingId = ExistingId(input);

                return !await db.Categories
                    .FromSql($"""SELECT * FROM categories WHERE "UserId" = {userId} AND "Kind" = {storedKind} AND lower("Name") = lower({name})""")
                    .AnyAsync(category => category.Id != existingId, cancellationToken);
            })
            .WithErrorCode("validation.categoryNameTaken")
            .When(input => CategoryInputParser.TryParseName(input.Name, out _)
                && CategoryWire.TryParse(input.Kind, out CategoryKind _));
    }

    protected abstract Guid? ExistingId(T input);
}
