using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.AddMember;

public static class AddMemberEndpoint
{
    public static RouteGroupBuilder MapAddMember(this RouteGroupBuilder group)
    {
        group.MapPost(
                "/groups/{id:guid}/members",
                async (Guid id, AddMemberRequest? request, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new AddMemberCommand(id, request?.Name), ct))
                        .ToHttp(member => Results.Created((string?)null, member)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
