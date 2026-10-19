using System.Text.Json;
using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Caching;
using Mita.Api.Common.Categories;

namespace Mita.Api.Features.Categories.CreateCategory;

public sealed record CreateCategoryRequest(JsonElement? Name, JsonElement? Kind, JsonElement? Color);

public sealed record CreateCategoryCommand(JsonElement? Name, JsonElement? Kind, JsonElement? Color)
    : IRequest<Result<CategoryDto>>, ICategoryInput, IInvalidatesCache
{
    public IReadOnlyCollection<string> CacheTagsToInvalidate => [CategoryCacheTags.Categories];
}
