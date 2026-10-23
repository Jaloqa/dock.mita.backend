using MediatR;
using Microsoft.AspNetCore.Mvc;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.CreateGroupRepayment;

public static class CreateGroupRepaymentEndpoint
{
    public static RouteGroupBuilder MapCreateGroupRepayment(this RouteGroupBuilder group)
    {
        group.MapPost(
                "/groups/{id:guid}/repayments",
                (Guid id, CreateGroupRepaymentRequest? request, IMediator mediator, CancellationToken ct) =>
                    CreateAsync(GroupScope.User(id), request, mediator, ct))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }

    public static RouteGroupBuilder MapSharedCreateGroupRepayment(this RouteGroupBuilder group)
    {
        group.MapPost(
            "/repayments",
            (
                string token,
                [FromHeader(Name = GroupAccess.MemberHeader)] string? member,
                CreateGroupRepaymentRequest? request,
                IMediator mediator,
                CancellationToken ct) => CreateAsync(GroupScope.Link(token, member), request, mediator, ct));

        return group;
    }

    private static async Task<IResult> CreateAsync(
        GroupScope scope,
        CreateGroupRepaymentRequest? request,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new CreateGroupRepaymentCommand(
            scope,
            request?.FromMemberId,
            request?.ToMemberId,
            request?.Amount,
            request?.Currency,
            request?.Date,
            request?.Rate,
            request?.Note);

        return (await mediator.Send(command, ct)).ToHttp(repayment => Results.Created((string?)null, repayment));
    }
}
