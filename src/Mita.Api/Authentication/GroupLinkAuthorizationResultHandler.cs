using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Mita.Api.Common;

namespace Mita.Api.Authentication;

public sealed class GroupLinkAuthorizationResultHandler(AuthorizationMiddlewareResultHandler fallback)
    : IAuthorizationMiddlewareResultHandler
{
    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (await RefusesLinkAsync(context, policy, authorizeResult))
        {
            await ProblemDetailsWriter.WriteStatusAsync(context, StatusCodes.Status404NotFound);
            return;
        }

        await fallback.HandleAsync(next, context, policy, authorizeResult);
    }

    // The policy evaluator challenges an anonymous caller without any failure details. A policy made of link
    // requirements alone failed on the link then; a policy that also needs a signed-in user checks the link again on
    // its own, so an unknown link answers 404 before a missing session answers 401.
    private static async Task<bool> RefusesLinkAsync(
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult result)
    {
        if (!(result.Challenged || result.Forbidden) || !policy.Requirements.OfType<GroupLinkRequirement>().Any())
        {
            return false;
        }

        if (result.AuthorizationFailure is { } failure)
        {
            return failure.FailedRequirements.OfType<GroupLinkRequirement>().Any();
        }

        if (policy.Requirements.All(requirement => requirement is GroupLinkRequirement))
        {
            return true;
        }

        var authorization = context.RequestServices.GetRequiredService<IAuthorizationService>();
        var link = await authorization.AuthorizeAsync(context.User, context, new GroupLinkRequirement());
        return !link.Succeeded;
    }
}
