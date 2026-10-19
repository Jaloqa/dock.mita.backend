using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Caching;
using Mita.Api.Common.Categories;

namespace Mita.Api.Features.Categories.DeleteCategory;

public sealed record DeleteCategoryCommand(Guid Id) : IRequest<Result>, IInvalidatesCache
{
    public IReadOnlyCollection<string> CacheTagsToInvalidate => [CategoryCacheTags.Categories];
}
