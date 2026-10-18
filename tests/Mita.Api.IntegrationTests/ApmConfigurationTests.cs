using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Mita.Api.IntegrationTests;

public sealed class ApmConfigurationTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private static readonly string[] AgentDefaults =
    [
        "password", "passwd", "pwd", "secret", "*key", "*token*", "*session*", "*credit*", "*card*", "*auth*",
        "set-cookie", "*principal*"
    ];

    [Fact]
    public void Should_RedactAgentDefaultsAndSessionCookies_When_ApmCapturesHeaders()
    {
        var configuration = factory.Services.GetRequiredService<IConfiguration>();

        var sanitized = configuration["ElasticApm:SanitizeFieldNames"].ShouldNotBeNull()
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        sanitized.ShouldBe([.. AgentDefaults, "cookie", "mita_*", "xsrf*"], ignoreOrder: true);
    }

    [Fact]
    public void Should_NotCaptureBodies_When_ApmEnabled() =>
        factory.Services.GetRequiredService<IConfiguration>()["ElasticApm:CaptureBody"].ShouldBe("off");
}
