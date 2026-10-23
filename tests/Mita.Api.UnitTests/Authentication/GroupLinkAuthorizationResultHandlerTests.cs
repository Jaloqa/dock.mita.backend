using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.Authentication;
using Moq;
using Shouldly;

namespace Mita.Api.UnitTests.Authentication;

public sealed class GroupLinkAuthorizationResultHandlerTests
{
    [Fact]
    public async Task Should_ReturnNotFound_When_LinkPolicyChallengesAnonymousCaller()
    {
        var (context, authentication) = await HandleAsync(LinkPolicy(), PolicyAuthorizationResult.Challenge());

        context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        authentication.Verify(
            service => service.ChallengeAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<AuthenticationProperties>()),
            Times.Never);
    }

    [Fact]
    public async Task Should_Challenge_When_CombinedPolicyChallengesAnonymousCaller()
    {
        var (context, authentication) = await HandleAsync(CombinedPolicy(), PolicyAuthorizationResult.Challenge());

        context.Response.StatusCode.ShouldNotBe(StatusCodes.Status404NotFound);
        authentication.Verify(
            service => service.ChallengeAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<AuthenticationProperties>()),
            Times.Once);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_LinkRequirementFails()
    {
        var (context, authentication) = await HandleAsync(
            CombinedPolicy(),
            PolicyAuthorizationResult.Forbid(AuthorizationFailure.Failed([new GroupLinkRequirement()])));

        context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        authentication.Verify(
            service => service.ForbidAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<AuthenticationProperties>()),
            Times.Never);
    }

    [Fact]
    public async Task Should_Forbid_When_OnlyOtherRequirementFails()
    {
        var (context, authentication) = await HandleAsync(
            CombinedPolicy(),
            PolicyAuthorizationResult.Forbid(
                AuthorizationFailure.Failed([new DenyAnonymousAuthorizationRequirement()])));

        context.Response.StatusCode.ShouldNotBe(StatusCodes.Status404NotFound);
        authentication.Verify(
            service => service.ForbidAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<AuthenticationProperties>()),
            Times.Once);
    }

    private static AuthorizationPolicy LinkPolicy() =>
        new AuthorizationPolicyBuilder().AddRequirements(new GroupLinkRequirement()).Build();

    private static AuthorizationPolicy CombinedPolicy() =>
        new AuthorizationPolicyBuilder().AddRequirements(new GroupLinkRequirement()).RequireAuthenticatedUser().Build();

    private static async Task<(HttpContext Context, Mock<IAuthenticationService> Authentication)> HandleAsync(
        AuthorizationPolicy policy,
        PolicyAuthorizationResult result)
    {
        var authentication = new Mock<IAuthenticationService>();
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .AddSingleton(authentication.Object)
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();

        await new GroupLinkAuthorizationResultHandler(new AuthorizationMiddlewareResultHandler())
            .HandleAsync(_ => Task.CompletedTask, context, policy, result);

        return (context, authentication);
    }
}
