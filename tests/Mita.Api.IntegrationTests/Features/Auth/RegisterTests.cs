using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Users;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Auth;

public sealed class RegisterTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_CreateUserSettingsAndCookies_When_Registering()
    {
        using var client = new AuthClient(factory);
        var credentials = AuthClient.NewCredentials() with { Name = "  Ada Lovelace  " };

        using var response = await client.RegisterAsync(credentials, "pl-PL,pl;q=0.9,en;q=0.8", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync(Ct);
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        root.EnumerateObject().Select(property => property.Name)
            .ShouldBe(["id", "name", "email", "createdAt"], ignoreOrder: true);
        var userId = root.GetProperty("id").GetGuid();
        root.GetProperty("name").GetString().ShouldBe("Ada Lovelace");
        root.GetProperty("email").GetString().ShouldBe(credentials.Email);
        root.GetProperty("createdAt").GetString()!.ShouldMatch(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$");

        var setCookies = SetCookieHeaderValue.ParseList(response.Headers.GetValues(HeaderNames.SetCookie).ToList());
        var access = setCookies.Single(cookie => cookie.Name == "mita_at");
        var refresh = setCookies.Single(cookie => cookie.Name == "mita_rt");
        var xsrf = setCookies.Single(cookie => cookie.Name == "XSRF-TOKEN");
        access.HttpOnly.ShouldBeTrue();
        access.Path.ToString().ShouldBe("/");
        refresh.HttpOnly.ShouldBeTrue();
        refresh.Secure.ShouldBeTrue();
        refresh.Path.ToString().ShouldBe("/v1/auth");
        xsrf.HttpOnly.ShouldBeFalse();
        xsrf.Path.ToString().ShouldBe("/");
        client.Cookie("mita_at").ShouldNotBeNull();
        client.Cookie("mita_rt", "/v1/auth/").ShouldNotBeNull();
        client.Xsrf.ShouldNotBeNullOrWhiteSpace();

        body.ShouldNotContain(access.Value.ToString());
        body.ShouldNotContain(refresh.Value.ToString());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.UserSettings.AsNoTracking().SingleAsync(row => row.UserId == userId, Ct);
        settings.Locale.ShouldBe(Locale.Pl);
        settings.BaseCurrency.ShouldBe(Currency.PLN);
        settings.Theme.ShouldBe(ThemePreference.System);
        var user = await db.Users.AsNoTracking().SingleAsync(row => row.Id == userId, Ct);
        user.LockoutEnabled.ShouldBeTrue();
        user.UserName.ShouldBe(credentials.Email);
    }

    [Theory]
    [InlineData("o'brien+tag@example.com")]
    [InlineData("zoë@x.de")]
    public async Task Should_Register_When_EmailHasCharactersOutsideIdentityDefaults(string email)
    {
        using var client = new AuthClient(factory);

        using var response = await client.RegisterAsync(AuthClient.NewCredentials() with { Email = email }, ct: Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("email").GetString().ShouldBe(email);
    }

    [Theory]
    [InlineData(null, "en")]
    [InlineData("de-DE,de;q=0.9", "en")]
    [InlineData("de-DE,uk;q=0.5,ru;q=0.7", "ru")]
    [InlineData("UK", "uk")]
    public async Task Should_PickLocaleFromAcceptLanguage_When_Registering(string? acceptLanguage, string expected)
    {
        using var client = new AuthClient(factory);

        using var response = await client.RegisterAsync(AuthClient.NewCredentials(), acceptLanguage, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var userId = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var settings = await scope.ServiceProvider.GetRequiredService<AppDbContext>().UserSettings.AsNoTracking()
            .SingleAsync(row => row.UserId == userId, Ct);
        settings.Locale.ToString().ToLowerInvariant().ShouldBe(expected);
    }

    [Fact]
    public async Task Should_ReturnFieldErrors_When_RegisterInvalid()
    {
        using var client = new AuthClient(factory);

        var missing = await ErrorsAsync(client, new { email = "valid@example.com", password = "long-enough-password" });
        missing["name"].ShouldBe(["validation.required"]);

        var whitespace = await ErrorsAsync(client, new { name = "   ", email = "   ", password = "" });
        whitespace["name"].ShouldBe(["validation.required"]);
        whitespace["email"].ShouldBe(["validation.required"]);
        whitespace["password"].ShouldBe(["validation.required"]);

        var invalid = await ErrorsAsync(client, new { name = new string('a', 41), email = "a@b", password = "123456789" });
        invalid["name"].ShouldBe(["validation.nameTooLong"]);
        invalid["email"].ShouldBe(["validation.invalidEmail"]);
        invalid["password"].ShouldBe(["validation.passwordTooShort"]);

        var tooLong = await ErrorsAsync(
            client,
            new { name = "  " + new string('a', 40) + "  ", email = new string('a', 243) + "@example.com", password = new string('p', 129) });
        tooLong.ContainsKey("name").ShouldBeFalse();
        tooLong["email"].ShouldBe(["validation.invalidEmail"]);
        tooLong["password"].ShouldBe(["validation.passwordTooLong"]);

        var empty = await ErrorsAsync(client, new { });
        empty["name"].ShouldBe(["validation.required"]);
        empty["email"].ShouldBe(["validation.required"]);
        empty["password"].ShouldBe(["validation.required"]);
    }

    [Fact]
    public async Task Should_ReturnEmailTaken_When_SameEmailDifferentCase()
    {
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);

        using var second = new AuthClient(factory);
        var errors = await ErrorsAsync(
            second,
            new { name = "Other", email = "  " + credentials.Email.ToUpperInvariant() + " ", password = "another-password" });

        errors["email"].ShouldBe(["validation.emailTaken"]);
        second.Cookie("mita_at").ShouldBeNull();
    }

    [Fact]
    public async Task Should_CreateOneUser_When_TwoRegistrationsRace()
    {
        var email = $"{Guid.NewGuid():N}@race.example.com";
        var variants = Enumerable.Range(0, 10)
            .Select(index => index % 2 == 0 ? email.ToUpperInvariant() : email)
            .Select((address, index) => index % 3 == 0 ? char.ToUpperInvariant(address[0]) + address[1..] : address)
            .ToList();

        var responses = await Task.WhenAll(variants.Select(async address =>
        {
            using var client = new AuthClient(factory);
            using var response = await client.RegisterAsync(
                new { name = "Racer", email = address, password = "race-password-123" },
                ct: Ct);
            return (response.StatusCode, Body: await response.Content.ReadAsStringAsync(Ct));
        }));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        var rejected = responses.Where(r => r.StatusCode != HttpStatusCode.Created).ToList();
        rejected.ShouldAllBe(r => r.StatusCode == HttpStatusCode.BadRequest);
        foreach (var (_, body) in rejected)
        {
            using var json = JsonDocument.Parse(body);
            json.RootElement.GetProperty("errors").GetProperty("email")[0].GetString().ShouldBe("validation.emailTaken");
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var normalized = email.ToUpperInvariant();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users
            .CountAsync(user => user.NormalizedEmail == normalized, Ct)).ShouldBe(1);
    }

    private static async Task<Dictionary<string, string[]>> ErrorsAsync(AuthClient client, object body)
    {
        using var response = await client.RegisterAsync(body, ct: Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return problem.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!;
    }
}
