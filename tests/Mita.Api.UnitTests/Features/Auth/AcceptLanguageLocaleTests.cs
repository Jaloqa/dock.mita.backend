using Microsoft.Net.Http.Headers;
using Mita.Api.Features.Auth.Register;
using Mita.Domain.Users;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Auth;

public sealed class AcceptLanguageLocaleTests
{
    [Theory]
    [InlineData("", Locale.En)]
    [InlineData("pl-PL", Locale.Pl)]
    [InlineData("de-DE,de;q=0.9", Locale.En)]
    [InlineData("de-DE,uk;q=0.5,ru;q=0.7", Locale.Ru)]
    [InlineData("en;q=0.1,pl;q=0.2", Locale.Pl)]
    [InlineData("pl;q=0,uk", Locale.Uk)]
    [InlineData("RU-ru", Locale.Ru)]
    [InlineData("*", Locale.En)]
    public void Should_PickHighestQualitySupportedLocale_When_Resolving(string header, Locale expected)
    {
        var values = StringWithQualityHeaderValue.ParseList(header.Length == 0 ? [] : [header]);

        AcceptLanguageLocale.Resolve(values).ShouldBe(expected);
    }
}
