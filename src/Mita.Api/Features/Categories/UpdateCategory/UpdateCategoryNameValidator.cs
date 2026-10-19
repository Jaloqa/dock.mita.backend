using Mita.Api.Authentication;
using Mita.Api.Common.Categories;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Categories.UpdateCategory;

public sealed class UpdateCategoryNameValidator(AppDbContext db, ICurrentUser currentUser)
    : CategoryNameValidator<UpdateCategoryCommand>(db, currentUser)
{
    protected override Guid? ExistingId(UpdateCategoryCommand input) => input.Id;
}
