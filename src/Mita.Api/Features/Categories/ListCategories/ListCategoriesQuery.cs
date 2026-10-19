using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Caching;
using Mita.Api.Common.Categories;

namespace Mita.Api.Features.Categories.ListCategories;

public sealed record ListCategoriesQuery(string? Kind, int Page, int PageSize)
    : IRequest<Result<PageResponse<CategoryDto>>>, ICachedQuery
{
    public string CacheKey => $"categories:{Kind ?? "*"}:{Page}:{PageSize}";

    public IReadOnlyCollection<string> CacheTags => [CategoryCacheTags.Categories];

    public TimeSpan? CacheExpiration => null;
}
