using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Mita.Domain.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Authentication;

public sealed class GroupLinkAuthorizationHandler(AppDbContext db)
    : AuthorizationHandler<GroupLinkRequirement, HttpContext>
{
    private const string TokenRouteValue = "token";

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        GroupLinkRequirement requirement,
        HttpContext resource)
    {
        var token = resource.GetRouteValue(TokenRouteValue) as string;
        if (!ShareTokens.IsWellFormed(token))
        {
            return;
        }

        if (await db.Groups.AsNoTracking().AnyAsync(group => group.ShareToken == token, resource.RequestAborted))
        {
            context.Succeed(requirement);
        }
    }
}
