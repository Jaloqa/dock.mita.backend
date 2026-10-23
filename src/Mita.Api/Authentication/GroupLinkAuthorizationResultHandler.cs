using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Mita.Api.Common;

namespace Mita.Api.Authentication;

public sealed class GroupLinkAuthorizationResultHandler(AuthorizationMiddlewareResultHandler fallback)
    : IAuthorizationMiddlewareResultHandler
{
    public Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult) =>
        RefusesLink(policy, authorizeResult)
            ? ProblemDetailsWriter.WriteStatusAsync(context, StatusCodes.Status404NotFound)
            : fallback.HandleAsync(next, context, policy, authorizeResult);

    // The policy evaluator challenges an anonymous caller without any failure details, so only a policy made of
    // link requirements alone answers 404 then; a policy that also needs a signed-in user keeps its 401.
    private static bool RefusesLink(AuthorizationPolicy policy, PolicyAuthorizationResult result) =>
        (result.Challenged || result.Forbidden)
        && (result.AuthorizationFailure is { } failure
            ? failure.FailedRequirements.OfType<GroupLinkRequirement>().Any()
            : policy.Requirements.All(requirement => requirement is GroupLinkRequirement));
}
