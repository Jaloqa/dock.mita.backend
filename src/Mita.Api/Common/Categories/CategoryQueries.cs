using Microsoft.EntityFrameworkCore;
using Mita.Domain.Categories;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Categories;

public static class CategoryQueries
{
    public static IQueryable<CategoryDto> ProjectCategories(this AppDbContext db, Guid userId, CategoryKind? kind = null) =>
        db.Categories.AsNoTracking()
            .Where(category => category.UserId == userId && (kind == null || category.Kind == kind))
            .Project(db);

    public static IQueryable<CategoryDto> ProjectCategory(this AppDbContext db, Guid userId, Guid categoryId) =>
        db.Categories.AsNoTracking()
            .Where(category => category.UserId == userId && category.Id == categoryId)
            .Project(db);

    private static IQueryable<CategoryDto> Project(this IQueryable<Category> categories, AppDbContext db) =>
        categories
            .OrderBy(category => category.Kind == CategoryKind.Expense ? 0 : 1)
            .ThenBy(category => category.System == null ? 0 : category.System == SystemCategory.Other ? 1 : 2)
            .ThenBy(category => category.CreatedAt)
            .ThenBy(category => category.Position)
            .ThenBy(category => category.Id)
            .Select(category => new CategoryDto(
                category.Id,
                category.Name,
                CategoryWire.ToWire(category.Kind),
                CategoryWire.ToWire(category.Color),
                CategoryWire.ToWire(category.System),
                db.Transactions.Any(transaction => transaction.CategoryId == category.Id)));
}
