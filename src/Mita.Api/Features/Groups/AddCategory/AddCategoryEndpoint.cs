using MediatR;
using Microsoft.AspNetCore.Mvc;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.AddCategory;

public static class AddCategoryEndpoint
{
    public static RouteGroupBuilder MapAddCategory(this RouteGroupBuilder group)
    {
        group.MapPost(
                "/groups/{id:guid}/categories",
                (Guid id, AddCategoryRequest? request, IMediator mediator, CancellationToken ct) =>
                    AddAsync(GroupScope.User(id), request, mediator, ct))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }

    public static RouteGroupBuilder MapSharedAddCategory(this RouteGroupBuilder group)
    {
        group.MapPost(
            "/categories",
            (
                string token,
                [FromHeader(Name = GroupAccess.MemberHeader)] string? member,
                AddCategoryRequest? request,
                IMediator mediator,
                CancellationToken ct) => AddAsync(GroupScope.Link(token, member), request, mediator, ct));

        return group;
    }

    private static async Task<IResult> AddAsync(
        GroupScope scope,
        AddCategoryRequest? request,
        IMediator mediator,
        CancellationToken ct) =>
        (await mediator.Send(new AddCategoryCommand(scope, request?.Name), ct))
            .ToHttp(category => Results.Created((string?)null, category));
}
