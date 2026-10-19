using Mita.Api.Extensions;
using Mita.Api.Features.Categories.CreateCategory;
using Mita.Api.Features.Categories.DeleteCategory;
using Mita.Api.Features.Categories.ListCategories;
using Mita.Api.Features.Categories.UpdateCategory;

namespace Mita.Api.Features.Categories;

public static class CategoriesEndpoints
{
    public static IEndpointRouteBuilder MapCategoriesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapV1()
            .WithTags("Categories")
            .MapListCategories()
            .MapCreateCategory()
            .MapUpdateCategory()
            .MapDeleteCategory();

        return app;
    }
}
