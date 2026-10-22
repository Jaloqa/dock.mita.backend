using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mita.Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Settings;

public sealed class SettingsTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string SettingsPath = "/v1/settings";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_Return401_When_NotAuthenticated()
    {
        using var client = new AuthClient(factory);

        using var response = await client.Http.GetAsync(SettingsPath, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_ReturnDefaults_ForNewUser()
    {
        using var client = new AuthClient(factory);
        using (await client.RegisterAsync(AuthClient.NewCredentials(), "pl-PL,pl;q=0.9", Ct))
        {
        }

        var body = await GetAsync(client);

        body.EnumerateObject().Select(property => property.Name)
            .ShouldBe(["baseCurrency", "locale", "monthStartDay", "theme", "notifications"], ignoreOrder: true);
        body.GetProperty("baseCurrency").GetString().ShouldBe("PLN");
        body.GetProperty("locale").GetString().ShouldBe("pl");
        body.GetProperty("monthStartDay").GetInt32().ShouldBe(1);
        body.GetProperty("theme").GetString().ShouldBe("system");
        var notifications = body.GetProperty("notifications");
        notifications.EnumerateObject().Select(property => property.Name)
            .ShouldBe(["budgetLimit", "dailyReminder", "weeklyReport", "goals"], ignoreOrder: true);
        notifications.GetProperty("budgetLimit").GetBoolean().ShouldBeTrue();
        notifications.GetProperty("dailyReminder").GetBoolean().ShouldBeTrue();
        notifications.GetProperty("weeklyReport").GetBoolean().ShouldBeFalse();
        notifications.GetProperty("goals").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Should_PersistUpdate()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        await GetAsync(client);

        using var response = await PutAsync(client, ValidBody(baseCurrency: "EUR", locale: "uk", monthStartDay: 15, theme: "dark"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var stored = await GetAsync(client);
        foreach (var body in new[] { updated, stored })
        {
            body.GetProperty("baseCurrency").GetString().ShouldBe("EUR");
            body.GetProperty("locale").GetString().ShouldBe("uk");
            body.GetProperty("monthStartDay").GetInt32().ShouldBe(15);
            body.GetProperty("theme").GetString().ShouldBe("dark");
            body.GetProperty("notifications").GetProperty("weeklyReport").GetBoolean().ShouldBeTrue();
            body.GetProperty("notifications").GetProperty("goals").GetBoolean().ShouldBeFalse();
        }
    }

    [Fact]
    public async Task Should_RoundTripLastWorkingDay()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        await GetAsync(client);

        using var response = await PutAsync(client, ValidBody(monthStartDay: "lastWorkingDay"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("monthStartDay").GetString()
            .ShouldBe("lastWorkingDay");
        (await GetAsync(client)).GetProperty("monthStartDay").GetString().ShouldBe("lastWorkingDay");

        using var back = await PutAsync(client, ValidBody(monthStartDay: 28));
        back.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetAsync(client)).GetProperty("monthStartDay").GetInt32().ShouldBe(28);
    }

    [Fact]
    public async Task Should_IsolateUsers()
    {
        using var userA = new AuthClient(factory);
        using var userB = new AuthClient(factory);
        await userA.RegisterNewAsync(Ct);
        await userB.RegisterNewAsync(Ct);

        using var updated = await PutAsync(userA, ValidBody(baseCurrency: "USD", theme: "dark"));
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetAsync(userA)).GetProperty("baseCurrency").GetString().ShouldBe("USD");

        var seenByB = await GetAsync(userB);
        seenByB.GetProperty("baseCurrency").GetString().ShouldBe("PLN");
        seenByB.GetProperty("theme").GetString().ShouldBe("system");

        using var updatedByB = await PutAsync(userB, ValidBody(baseCurrency: "EUR"));
        updatedByB.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetAsync(userA)).GetProperty("baseCurrency").GetString().ShouldBe("USD");
        (await GetAsync(userB)).GetProperty("baseCurrency").GetString().ShouldBe("EUR");
    }

    [Fact]
    public async Task Should_Return400WithFieldErrors_When_BodyInvalid()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        var errors = await ErrorsAsync(client, ValidBody(baseCurrency: "GBP", monthStartDay: 31));

        errors["baseCurrency"].ShouldBe(["validation.invalidValue"]);
        errors["monthStartDay"].ShouldBe(["validation.invalidValue"]);
        errors.Keys.ShouldBe(["baseCurrency", "monthStartDay"], ignoreOrder: true);
        (await GetAsync(client)).GetProperty("baseCurrency").GetString().ShouldBe("PLN");
    }

    [Fact]
    public async Task Should_RejectNonBaseCurrency_When_UpdatingSettings()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        var errors = await ErrorsAsync(client, ValidBody(baseCurrency: "JPY"));

        errors["baseCurrency"].ShouldBe(["validation.invalidValue"]);
        (await GetAsync(client)).GetProperty("baseCurrency").GetString().ShouldBe("PLN");
    }

    [Fact]
    public async Task Should_ReturnInvalidValue_When_BaseCurrencyIsNumber()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        var errors = await ErrorsAsync(client, ValidBody(baseCurrency: 5));

        errors["baseCurrency"].ShouldBe(["validation.invalidValue"]);
    }

    [Fact]
    public async Task Should_ReturnRequired_When_FieldMissingOrNull()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        var missing = await ErrorsAsync(client, new { baseCurrency = "PLN", locale = "en", theme = "system" });
        missing.Keys.ShouldBe(["monthStartDay", "notifications"], ignoreOrder: true);
        missing["monthStartDay"].ShouldBe(["validation.required"]);
        missing["notifications"].ShouldBe(["validation.required"]);

        var withNull = ValidBody();
        withNull["locale"] = null;
        var nulls = await ErrorsAsync(client, withNull);
        nulls["locale"].ShouldBe(["validation.required"]);

        var empty = await ErrorsAsync(client, ValidBody(theme: ""));
        empty["theme"].ShouldBe(["validation.invalidValue"]);
    }

    [Fact]
    public async Task Should_IgnoreUnknownProperties()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var response = await PutAsync(client, new
        {
            baseCurrency = "PLN",
            locale = "en",
            monthStartDay = 1,
            theme = "system",
            unknown = 1,
            notifications = new { budgetLimit = true, dailyReminder = true, weeklyReport = true, goals = true, extra = true }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.TryGetProperty("unknown", out _).ShouldBeFalse();
        body.GetProperty("notifications").TryGetProperty("extra", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Reject_When_XsrfMissing()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        using var request = new HttpRequestMessage(HttpMethod.Put, SettingsPath) { Content = JsonContent.Create(ValidBody()) };

        using var response = await client.Http.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static Dictionary<string, object?> ValidBody(
        object? baseCurrency = null,
        object? locale = null,
        object? monthStartDay = null,
        object? theme = null) => new Dictionary<string, object?>
        {
            ["baseCurrency"] = baseCurrency ?? "PLN",
            ["locale"] = locale ?? "en",
            ["monthStartDay"] = monthStartDay ?? 1,
            ["theme"] = theme ?? "system",
            ["notifications"] = new { budgetLimit = false, dailyReminder = true, weeklyReport = true, goals = false }
        };

    private static async Task<JsonElement> GetAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync(SettingsPath, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static Task<HttpResponseMessage> PutAsync(AuthClient client, object body)
    {
        var request = client.WithXsrf(new HttpRequestMessage(HttpMethod.Put, SettingsPath) { Content = JsonContent.Create(body) });
        return client.Http.SendAsync(request, Ct);
    }

    private static async Task<Dictionary<string, string[]>> ErrorsAsync(AuthClient client, object body)
    {
        using var response = await PutAsync(client, body);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return problem.GetProperty("errors").EnumerateObject()
            .ToDictionary(
                property => property.Name,
                property => property.Value.EnumerateArray().Select(item => item.GetString()!).ToArray());
    }
}