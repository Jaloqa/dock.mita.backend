using System.Text.Json;
using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Caching;
using Mita.Api.Common.Categories;

namespace Mita.Api.Features.Categories.UpdateCategory;

public sealed record UpdateCategoryRequest(JsonElement? Name, JsonElement? Kind, JsonElement? Color);

public sealed record UpdateCategoryCommand(Guid Id, JsonElement? Name, JsonElement? Kind, JsonElement? Color)
    : IRequest<Result<CategoryDto>>, ICategoryInput, IInvalidatesCache
{
    public IReadOnlyCollection<string> CacheTagsToInvalidate => [CategoryCacheTags.Categories];
}
