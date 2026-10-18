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

    private static LogEvent Event(params LogEventProperty[] properties) => new(
        DateTimeOffset.UtcNow,
        LogEventLevel.Information,
        null,
        new MessageTemplateParser().Parse("Test"),
        properties);
}
