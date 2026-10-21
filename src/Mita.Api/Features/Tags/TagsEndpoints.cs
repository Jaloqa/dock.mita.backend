using Mita.Api.Extensions;
using Mita.Api.Features.Tags.DeleteTag;
using Mita.Api.Features.Tags.ListTags;
using Mita.Api.Features.Tags.RenameTag;

namespace Mita.Api.Features.Tags;

public static class TagsEndpoints
{
    public static IEndpointRouteBuilder MapTagsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapV1()
            .WithTags("Tags")
            .MapListTags()
            .MapRenameTag()
            .MapDeleteTag();

        return app;
    }
}
