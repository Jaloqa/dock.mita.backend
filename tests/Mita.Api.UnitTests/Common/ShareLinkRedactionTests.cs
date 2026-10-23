using Mita.Api.Common.Logging;
using Shouldly;

namespace Mita.Api.UnitTests.Common;

public sealed class ShareLinkRedactionTests
{
    private const string Token = "Zm9v-YmFy_LWJhei1xdXV4LTAxMjM0NTY3ODlfYWJjZ";

    [Theory]
    [InlineData("/v1/shared/groups/" + Token, "/v1/shared/groups/***")]
    [InlineData(
        "/v1/shared/groups/" + Token + "/expenses/0f8fad5b-d9cb-469f-a165-70867728950e/history?deleted=true",
        "/v1/shared/groups/***/expenses/0f8fad5b-d9cb-469f-a165-70867728950e/history?deleted=true")]
    [InlineData("/v1/shared/groups/" + Token + "?to=EUR", "/v1/shared/groups/***?to=EUR")]
    [InlineData(
        "https://api.example.com/v1/shared/groups/" + Token + "/rates?to=EUR",
        "https://api.example.com/v1/shared/groups/***/rates?to=EUR")]
    [InlineData("GET /V1/Shared/Groups/" + Token + "/members", "GET /V1/Shared/Groups/***/members")]
    public void Should_MaskToken_When_PathIsLinkPath(string path, string expected) =>
        ShareLinkRedaction.Redact(path).ShouldBe(expected);

    [Theory]
    [InlineData("/v1/groups/0f8fad5b-d9cb-469f-a165-70867728950e/share")]
    [InlineData("/v1/auth/login")]
    [InlineData("/v1/shared/groups/")]
    [InlineData("")]
    public void Should_KeepPath_When_PathIsNoLinkPath(string path) =>
        ShareLinkRedaction.Redact(path).ShouldBe(path);

    [Fact]
    public void Should_ReturnNull_When_PathIsNull() =>
        ShareLinkRedaction.Redact(null).ShouldBeNull();

    [Theory]
    [InlineData("/v1/shared/groups/" + Token)]
    [InlineData("/v1/shared/groups/" + Token + "/expenses?deleted=true")]
    [InlineData("GET /V1/Shared/Groups/" + Token + "/members")]
    public void Should_FindToken_When_PathIsLinkPath(string path) =>
        ShareLinkRedaction.ContainsToken(path).ShouldBeTrue();

    [Theory]
    [InlineData("/v1/groups/0f8fad5b-d9cb-469f-a165-70867728950e/share")]
    [InlineData("/v1/shared/groups/")]
    [InlineData("")]
    [InlineData(null)]
    public void Should_FindNoToken_When_PathIsNoLinkPath(string? path) =>
        ShareLinkRedaction.ContainsToken(path).ShouldBeFalse();
}
