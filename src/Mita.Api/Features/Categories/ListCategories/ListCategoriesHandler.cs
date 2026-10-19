using MediatR;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Categories;
using Mita.Domain.Categories;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Categories.ListCategories;

public sealed class ListCategoriesHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ListCategoriesQuery, Result<PageResponse<CategoryDto>>>
{
    public async Task<Result<PageResponse<CategoryDto>>> Handle(ListCategoriesQuery query, CancellationToken cancellationToken)
    {
        CategoryKind? kind = CategoryWire.TryParse(query.Kind, out CategoryKind parsed) ? parsed : null;

        return await db.ProjectCategories(currentUser.Id, kind)
            .ToPageAsync(new PageRequest(query.Page, query.PageSize), cancellationToken);
    }
}
