using Mita.Api.Common.Logging;
using Moq;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Shouldly;

namespace Mita.Api.UnitTests.Common;

public sealed class SensitiveDataMaskingEnricherTests
{
    private const string Secret = "super-secret-value";

    [Fact]
    public void Should_MaskSensitiveProperties_When_TopLevelOrNested()
    {
        var logEvent = Event(
            new LogEventProperty("Password", new ScalarValue(Secret)),
            new LogEventProperty("RefreshToken", new ScalarValue(Secret)),
            new LogEventProperty("Command", new StructureValue(
                [
                    new LogEventProperty("Email", new ScalarValue("a@example.com")),
                    new LogEventProperty("Password", new ScalarValue(Secret))
                ],
                "RegisterCommand")),
            new LogEventProperty("RequestHeaders", new DictionaryValue(
                [
                    new(new ScalarValue("Cookie"), new ScalarValue($"mita_rt={Secret}")),
                    new(new ScalarValue("Set-Cookie"), new ScalarValue($"mita_at={Secret}")),
                    new(new ScalarValue("Accept"), new ScalarValue("application/json"))
                ])),
            new LogEventProperty("Items", new SequenceValue(
                [new StructureValue([new LogEventProperty("token", new ScalarValue(Secret))])])),
            new LogEventProperty("RequestPath", new ScalarValue("/v1/auth/login")));

        new SensitiveDataMaskingEnricher().Enrich(logEvent, Mock.Of<ILogEventPropertyFactory>());

        var rendered = string.Join(' ', logEvent.Properties.Select(property => $"{property.Key}={property.Value}"));
        rendered.ShouldNotContain(Secret);
        rendered.ShouldContain("a@example.com");
        rendered.ShouldContain("application/json");
        logEvent.Properties["RequestPath"].ToString().ShouldBe("\"/v1/auth/login\"");
        logEvent.Properties["Password"].ToString().ShouldBe("\"***\"");
    }

    [Fact]
    public void Should_MaskShareToken_When_PropertiesHoldLinkPath()
    {
        const string token = "Zm9v-YmFy_LWJhei1xdXV4LTAxMjM0NTY3ODlfYWJjZ";
        var logEvent = Event(
            new LogEventProperty("RequestPath", new ScalarValue($"/v1/shared/groups/{token}/expenses")),
            new LogEventProperty("Path", new ScalarValue($"/v1/shared/groups/{token}")),
            new LogEventProperty("RequestUri", new ScalarValue($"http://localhost/v1/shared/groups/{token}?to=EUR")),
            new LogEventProperty("Scope", new SequenceValue([new ScalarValue($"GET /v1/shared/groups/{token}")])),
            new LogEventProperty("Request", new StructureValue(
                [new LogEventProperty("Url", new ScalarValue($"/v1/shared/groups/{token}/balances"))])),
            new LogEventProperty("StatusCode", new ScalarValue(200)),
            new LogEventProperty("OwnerPath", new ScalarValue("/v1/groups/0f8fad5b-d9cb-469f-a165-70867728950e")));

        new SensitiveDataMaskingEnricher().Enrich(logEvent, Mock.Of<ILogEventPropertyFactory>());

        var rendered = string.Join(' ', logEvent.Properties.Select(property => $"{property.Key}={property.Value}"));
        rendered.ShouldNotContain(token);
        logEvent.Properties["RequestPath"].ToString().ShouldBe("\"/v1/shared/groups/***/expenses\"");
        logEvent.Properties["Path"].ToString().ShouldBe("\"/v1/shared/groups/***\"");
        logEvent.Properties["RequestUri"].ToString().ShouldBe("\"http://localhost/v1/shared/groups/***?to=EUR\"");
        logEvent.Properties["Scope"].ToString().ShouldBe("[\"GET /v1/shared/groups/***\"]");
        logEvent.Properties["Request"].ToString().ShouldBe("{ Url: \"/v1/shared/groups/***/balances\" }");
        logEvent.Properties["StatusCode"].ToString().ShouldBe("200");
        logEvent.Properties["OwnerPath"].ToString().ShouldBe("\"/v1/groups/0f8fad5b-d9cb-469f-a165-70867728950e\"");
    }

    [Fact]
    public void Should_MaskShareToken_When_PropertiesHoldLinkUri()
    {
        const string token = "Zm9v-YmFy_LWJhei1xdXV4LTAxMjM0NTY3ODlfYWJjZ";
        var ownerUri = new ScalarValue(new Uri("http://localhost/v1/groups/0f8fad5b-d9cb-469f-a165-70867728950e"));
        var logEvent = Event(
            new LogEventProperty(
                "RequestUri",
                new ScalarValue(new Uri($"http://localhost/v1/shared/groups/{token}/expenses?deleted=true"))),
            new LogEventProperty("Path", new ScalarValue(new Uri($"/v1/shared/groups/{token}", UriKind.Relative))),
            new LogEventProperty("Request", new StructureValue(
                [new LogEventProperty("Uri", new ScalarValue(new Uri($"http://localhost/v1/shared/groups/{token}")))])),
            new LogEventProperty("OwnerUri", ownerUri));

        new SensitiveDataMaskingEnricher().Enrich(logEvent, Mock.Of<ILogEventPropertyFactory>());

        var rendered = string.Join(' ', logEvent.Properties.Select(property => $"{property.Key}={property.Value}"));
        rendered.ShouldNotContain(token);
        logEvent.Properties["RequestUri"].ToString()
            .ShouldBe("\"http://localhost/v1/shared/groups/***/expenses?deleted=true\"");
        logEvent.Properties["Path"].ToString().ShouldBe("\"/v1/shared/groups/***\"");
        logEvent.Properties["Request"].ToString().ShouldBe("{ Uri: \"http://localhost/v1/shared/groups/***\" }");
        logEvent.Properties["OwnerUri"].ShouldBeSameAs(ownerUri);
    }

    private static LogEvent Event(params LogEventProperty[] properties) => new(
        DateTimeOffset.UtcNow,
        LogEventLevel.Information,
        null,
        new MessageTemplateParser().Parse("Test"),
        properties);
}
