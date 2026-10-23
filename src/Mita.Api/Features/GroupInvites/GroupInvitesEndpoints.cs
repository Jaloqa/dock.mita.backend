using Mita.Api.Extensions;
using Mita.Api.Features.GroupInvites.AcceptGroupInvite;
using Mita.Api.Features.GroupInvites.DeclineGroupInvite;
using Mita.Api.Features.GroupInvites.ListGroupInvites;

namespace Mita.Api.Features.GroupInvites;

public static class GroupInvitesEndpoints
{
    public static IEndpointRouteBuilder MapGroupInvitesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapV1()
            .WithTags("Group invites")
            .MapListGroupInvites()
            .MapAcceptGroupInvite()
            .MapDeclineGroupInvite();

        return app;
    }
}
