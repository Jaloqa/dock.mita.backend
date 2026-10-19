using Mita.Api.Extensions;
using Mita.Api.Features.Categories.CreateCategory;
using Mita.Api.Features.Categories.ListCategories;

namespace Mita.Api.Features.Categories;

public static class CategoriesEndpoints
{
    public static IEndpointRouteBuilder MapCategoriesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapV1()
            .WithTags("Categories")
            .MapListCategories()
            .MapCreateCategory();

        return app;
    }
}
