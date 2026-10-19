using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Categories;
using Mita.Domain.Categories;
using Mita.Infrastructure.Persistence;
using Npgsql;

namespace Mita.Api.Features.Categories.CreateCategory;

public sealed class CreateCategoryHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<CreateCategoryCommand, Result<CategoryDto>>
{
    private const string NameIndex = "IX_categories_user_kind_name";

    public async Task<Result<CategoryDto>> Handle(CreateCategoryCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var input = CategoryInputParser.Parse(command);
        var category = Category.Create(userId, input.Name, input.Kind, input.Color);

        db.Categories.Add(category);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: NameIndex
        })
        {
            return Error.Validation(new Dictionary<string, string[]> { ["name"] = ["validation.categoryNameTaken"] });
        }

        return await db.ProjectCategory(userId, category.Id).SingleAsync(cancellationToken);
    }
}
