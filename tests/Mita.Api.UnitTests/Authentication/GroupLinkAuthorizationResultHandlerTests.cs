using System.Security.Claims;
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
        var handled = await HandleAsync(LinkPolicy(), PolicyAuthorizationResult.Challenge());

        handled.Context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        handled.Authentication.Verify(
            service => service.ChallengeAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<AuthenticationProperties>()),
            Times.Never);
        VerifyLinkChecked(handled, Times.Never());
    }

    [Fact]
    public async Task Should_Challenge_When_CombinedPolicyChallengesAnonymousCallerWithKnownLink()
    {
        var handled = await HandleAsync(CombinedPolicy(), PolicyAuthorizationResult.Challenge(), linkKnown: true);

        handled.Context.Response.StatusCode.ShouldNotBe(StatusCodes.Status404NotFound);
        handled.Authentication.Verify(
            service => service.ChallengeAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<AuthenticationProperties>()),
            Times.Once);
        VerifyLinkChecked(handled, Times.Once());
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_CombinedPolicyChallengesAnonymousCallerWithUnknownLink()
    {
        var handled = await HandleAsync(CombinedPolicy(), PolicyAuthorizationResult.Challenge(), linkKnown: false);

        handled.Context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        handled.Authentication.Verify(
            service => service.ChallengeAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<AuthenticationProperties>()),
            Times.Never);
        VerifyLinkChecked(handled, Times.Once());
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_LinkRequirementFails()
    {
        var handled = await HandleAsync(
            CombinedPolicy(),
            PolicyAuthorizationResult.Forbid(AuthorizationFailure.Failed([new GroupLinkRequirement()])));

        handled.Context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        handled.Authentication.Verify(
            service => service.ForbidAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<AuthenticationProperties>()),
            Times.Never);
        VerifyLinkChecked(handled, Times.Never());
    }

    [Fact]
    public async Task Should_Forbid_When_OnlyOtherRequirementFails()
    {
        var handled = await HandleAsync(
            CombinedPolicy(),
            PolicyAuthorizationResult.Forbid(
                AuthorizationFailure.Failed([new DenyAnonymousAuthorizationRequirement()])));

        handled.Context.Response.StatusCode.ShouldNotBe(StatusCodes.Status404NotFound);
        handled.Authentication.Verify(
            service => service.ForbidAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<AuthenticationProperties>()),
            Times.Once);
        VerifyLinkChecked(handled, Times.Never());
    }

    [Fact]
    public async Task Should_Challenge_When_PolicyHasNoLinkRequirement()
    {
        var handled = await HandleAsync(
            new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build(),
            PolicyAuthorizationResult.Challenge());

        handled.Context.Response.StatusCode.ShouldNotBe(StatusCodes.Status404NotFound);
        handled.Authentication.Verify(
            service => service.ChallengeAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<AuthenticationProperties>()),
            Times.Once);
        VerifyLinkChecked(handled, Times.Never());
    }

    private static AuthorizationPolicy LinkPolicy() =>
        new AuthorizationPolicyBuilder().AddRequirements(new GroupLinkRequirement()).Build();

    private static AuthorizationPolicy CombinedPolicy() =>
        new AuthorizationPolicyBuilder().AddRequirements(new GroupLinkRequirement()).RequireAuthenticatedUser().Build();

    private static void VerifyLinkChecked(Handled handled, Times times) =>
        handled.Authorization.Verify(
            service => service.AuthorizeAsync(
                handled.Context.User,
                handled.Context,
                It.Is<IEnumerable<IAuthorizationRequirement>>(requirements =>
                    requirements.Count() == 1 && requirements.Single() is GroupLinkRequirement)),
            times);

    private static async Task<Handled> HandleAsync(
        AuthorizationPolicy policy,
        PolicyAuthorizationResult result,
        bool linkKnown = true)
    {
        var authentication = new Mock<IAuthenticationService>();
        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object?>(),
                It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(linkKnown ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .AddSingleton(authentication.Object)
            .AddSingleton(authorization.Object)
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();

        await new GroupLinkAuthorizationResultHandler(new AuthorizationMiddlewareResultHandler())
            .HandleAsync(_ => Task.CompletedTask, context, policy, result);

        return new Handled(context, authentication, authorization);
    }

    private sealed record Handled(
        HttpContext Context,
        Mock<IAuthenticationService> Authentication,
        Mock<IAuthorizationService> Authorization);
}
