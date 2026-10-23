using MediatR;
using Microsoft.AspNetCore.Mvc;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.UpdateGroupRepayment;

public static class UpdateGroupRepaymentEndpoint
{
    public static RouteGroupBuilder MapUpdateGroupRepayment(this RouteGroupBuilder group)
    {
        group.MapPut(
                "/groups/{id:guid}/repayments/{repaymentId:guid}",
                (
                    Guid id,
                    Guid repaymentId,
                    UpdateGroupRepaymentRequest? request,
                    IMediator mediator,
                    CancellationToken ct) => UpdateAsync(GroupScope.Owner(id), repaymentId, request, mediator, ct))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }

    public static RouteGroupBuilder MapSharedUpdateGroupRepayment(this RouteGroupBuilder group)
    {
        group.MapPut(
            "/repayments/{repaymentId:guid}",
            (
                string token,
                Guid repaymentId,
                [FromHeader(Name = GroupAccess.MemberHeader)] string? member,
                UpdateGroupRepaymentRequest? request,
                IMediator mediator,
                CancellationToken ct) =>
                UpdateAsync(GroupScope.Link(token, member), repaymentId, request, mediator, ct));

        return group;
    }

    private static async Task<IResult> UpdateAsync(
        GroupScope scope,
        Guid repaymentId,
        UpdateGroupRepaymentRequest? request,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new UpdateGroupRepaymentCommand(
            scope,
            repaymentId,
            request?.FromMemberId,
            request?.ToMemberId,
            request?.Amount,
            request?.Currency,
            request?.Date,
            request?.Rate,
            request?.Note);

        return (await mediator.Send(command, ct)).ToHttp(repayment => Results.Ok(repayment));
    }
}
