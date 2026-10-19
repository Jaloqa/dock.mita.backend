using Mita.Api.Authentication;
using Mita.Api.Common.Categories;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Categories.CreateCategory;

public sealed class CreateCategoryNameValidator(AppDbContext db, ICurrentUser currentUser)
    : CategoryNameValidator<CreateCategoryCommand>(db, currentUser)
{
    protected override Guid? ExistingId(CreateCategoryCommand input) => null;
}
