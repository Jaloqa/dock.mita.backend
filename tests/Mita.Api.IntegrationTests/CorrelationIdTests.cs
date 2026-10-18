using Mita.Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Mita.Api.IntegrationTests;

public sealed class CorrelationIdTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string HeaderName = "X-Correlation-Id";

    [Fact]
    public async Task Should_EchoCorrelationId_When_ClientSendsValidOne()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add(HeaderName, "abc-123");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.Headers.GetValues(HeaderName).ShouldHaveSingleItem().ShouldBe("abc-123");
    }

    [Fact]
    public async Task Should_GenerateCorrelationId_When_HeaderMissing()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        Guid.TryParse(response.Headers.GetValues(HeaderName).ShouldHaveSingleItem(), out _).ShouldBeTrue();
    }

    [Theory]
    [InlineData("long")]
    [InlineData("newline")]
    [InlineData("trailingNewline")]
    public async Task Should_ReplaceCorrelationId_When_ClientSendsInvalidOne(string kind)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        var invalid = kind switch
        {
            "long" => new string('a', 200),
            "newline" => "a\nb",
            _ => "abc\n"
        };
        request.Headers.TryAddWithoutValidation(HeaderName, invalid);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        var value = response.Headers.GetValues(HeaderName).ShouldHaveSingleItem();
        value.ShouldNotBe(invalid);
        Guid.TryParse(value, out _).ShouldBeTrue();
    }
}
