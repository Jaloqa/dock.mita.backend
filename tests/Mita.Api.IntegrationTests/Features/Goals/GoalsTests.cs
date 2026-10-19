using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Accounts;
using Mita.Domain.Money;
using Mita.Domain.Periods;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Goals;

public sealed class GoalsTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string GoalsPath = "/v1/goals";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static PeriodKey Current => PeriodKey.Of(Today);

    [Fact]
    public async Task Should_Return401_When_NotAuthenticated()
    {
        using var client = new AuthClient(factory);

        using var response = await client.Http.GetAsync(GoalsPath, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_CreateGoal_And_ListWithSummary()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var savings = await TestData.AddAccountAsync(factory, userId, type: AccountType.Savings);
        var deadline = Current.AddMonths(4).ToString();

        using var created = await SendAsync(client, HttpMethod.Post, GoalsPath, Body("  Wakacje  ", 800_000, deadline, null));
        using var linked = await SendAsync(client, HttpMethod.Post, GoalsPath, Body("Laptop", 500_000, null, savings.Id));

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.EnumerateObject().Select(property => property.Name).ShouldBe(
            ["id", "name", "target", "saved", "remaining", "percent", "monthly", "status", "deadline", "accountId", "currency"],
            ignoreOrder: true);
        var firstId = body.GetProperty("id").GetGuid();
        created.Headers.Location!.ToString().ShouldBe($"{GoalsPath}/{firstId}");
        body.GetProperty("name").GetString().ShouldBe("Wakacje");
        body.GetProperty("target").GetInt64().ShouldBe(800_000);
        body.GetProperty("saved").GetInt64().ShouldBe(0);
        body.GetProperty("remaining").GetInt64().ShouldBe(800_000);
        body.GetProperty("percent").GetInt32().ShouldBe(0);
        body.GetProperty("monthly").GetInt64().ShouldBe(200_000);
        body.GetProperty("status").GetString().ShouldBe("active");
        body.GetProperty("deadline").GetString().ShouldBe(deadline);
        body.GetProperty("accountId").ValueKind.ShouldBe(JsonValueKind.Null);
        body.GetProperty("currency").GetString().ShouldBe("PLN");

        linked.StatusCode.ShouldBe(HttpStatusCode.Created);
        var second = await linked.Content.ReadFromJsonAsync<JsonElement>(Ct);
        second.GetProperty("accountId").GetGuid().ShouldBe(savings.Id);
        second.GetProperty("deadline").ValueKind.ShouldBe(JsonValueKind.Null);
        second.GetProperty("monthly").ValueKind.ShouldBe(JsonValueKind.Null);

        using var list = await client.Http.GetAsync(GoalsPath, Ct);
        list.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await list.Content.ReadFromJsonAsync<JsonElement>(Ct);
        page.EnumerateObject().Select(property => property.Name).ShouldBe(
            ["items", "page", "pageSize", "total", "currency", "summary"],
            ignoreOrder: true);
        page.GetProperty("page").GetInt32().ShouldBe(1);
        page.GetProperty("pageSize").GetInt32().ShouldBe(100);
        page.GetProperty("total").GetInt32().ShouldBe(2);
        page.GetProperty("currency").GetString().ShouldBe("PLN");
        var summary = page.GetProperty("summary");
        summary.GetProperty("saved").GetInt64().ShouldBe(0);
        summary.GetProperty("monthly").GetInt64().ShouldBe(0);
        summary.GetProperty("active").GetInt32().ShouldBe(2);
        page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())
            .ShouldBe([firstId, second.GetProperty("id").GetGuid()]);
    }

    [Fact]
    public async Task Should_SummarizeEveryGoal_When_Paged()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var first = await TestData.AddGoalAsync(factory, userId, "First", 10_000, saved: 10_000);
        await TestData.AddGoalAsync(factory, userId, "Second", 10_000, saved: 2_500);
        await TestData.AddContributionAsync(factory, first, IsoDay(Today), 7_000);
        await TestData.AddContributionAsync(factory, first, IsoDay(Today.AddMonths(-3).AddDays(1)), 2_001);
        await TestData.AddContributionAsync(factory, first, IsoDay(Today.AddMonths(-3)), 50_000);
        await TestData.AddContributionAsync(factory, first, IsoDay(Today.AddDays(1)), 50_000);

        using var list = await client.Http.GetAsync($"{GoalsPath}?page=2&pageSize=1", Ct);

        var page = await list.Content.ReadFromJsonAsync<JsonElement>(Ct);
        page.GetProperty("total").GetInt32().ShouldBe(2);
        page.GetProperty("items").GetArrayLength().ShouldBe(1);
        page.GetProperty("items")[0].GetProperty("name").GetString().ShouldBe("Second");
        var summary = page.GetProperty("summary");
        summary.GetProperty("saved").GetInt64().ShouldBe(12_500);
        summary.GetProperty("monthly").GetInt64().ShouldBe(3_000);
        summary.GetProperty("active").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Should_ReportInvalidValue_When_PagingInvalid()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var response = await client.Http.GetAsync($"{GoalsPath}?pageSize=101", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldHaveErrorAsync(response, "pageSize", "validation.invalidValue");
    }

    [Fact]
    public async Task Should_ValidateDeadlineAndAccount()
    {
        using var client = new AuthClient(factory);
        using var other = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        await other.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var card = await TestData.AddAccountAsync(factory, userId);
        var foreign = await TestData.AddAccountAsync(factory, await UserIdAsync(other), type: AccountType.Savings);
        var archived = await TestData.AddAccountAsync(factory, userId, archived: true, type: AccountType.Savings);

        using var past = await SendAsync(client, HttpMethod.Post, GoalsPath, Body("Goal", 1_000, Current.AddMonths(-1).ToString(), null));
        using var tooFar = await SendAsync(client, HttpMethod.Post, GoalsPath, Body("Goal", 1_000, Current.AddMonths(601).ToString(), null));
        using var thisMonth = await SendAsync(client, HttpMethod.Post, GoalsPath, Body("Goal", 1_000, Current.ToString(), null));
        using var farthest = await SendAsync(client, HttpMethod.Post, GoalsPath, Body("Goal", 1_000, Current.AddMonths(600).ToString(), null));
        using var cardResponse = await SendAsync(client, HttpMethod.Post, GoalsPath, Body("Goal", 1_000, null, card.Id));
        using var foreignResponse = await SendAsync(client, HttpMethod.Post, GoalsPath, Body("Goal", 1_000, null, foreign.Id));
        using var textResponse = await SendAsync(
            client, HttpMethod.Post, GoalsPath, new { name = "Goal", target = 1_000, deadline = (string?)null, accountId = "acc_x" });
        using var archivedResponse = await SendAsync(client, HttpMethod.Post, GoalsPath, Body("Goal", 1_000, null, archived.Id));

        await ShouldHaveErrorAsync(past, "deadline", "validation.deadlinePast");
        await ShouldHaveErrorAsync(tooFar, "deadline", "validation.invalidValue");
        thisMonth.StatusCode.ShouldBe(HttpStatusCode.Created);
        farthest.StatusCode.ShouldBe(HttpStatusCode.Created);
        await ShouldHaveErrorAsync(cardResponse, "accountId", "validation.savingsAccountOnly");
        await ShouldHaveErrorAsync(foreignResponse, "accountId", "validation.unknownAccount");
        await ShouldHaveErrorAsync(textResponse, "accountId", "validation.unknownAccount");
        await ShouldHaveErrorAsync(archivedResponse, "accountId", "validation.archivedAccount");
    }

    [Fact]
    public async Task Should_KeepExistingLinkAndDeadline_When_Updating()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var savings = await TestData.AddAccountAsync(factory, userId, type: AccountType.Savings);
        using var created = await SendAsync(client, HttpMethod.Post, GoalsPath, Body("Car", 1_000_000, null, savings.Id));
        var goalId = (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
        await ArchiveAsync(savings.Id);

        using var keptLink = await SendAsync(client, HttpMethod.Put, $"{GoalsPath}/{goalId}", Body("Car", 1_200_000, null, savings.Id));

        keptLink.StatusCode.ShouldBe(HttpStatusCode.OK);
        var linked = await keptLink.Content.ReadFromJsonAsync<JsonElement>(Ct);
        linked.GetProperty("accountId").GetGuid().ShouldBe(savings.Id);
        linked.GetProperty("target").GetInt64().ShouldBe(1_200_000);

        var stored = Current.AddMonths(-2).ToString();
        var overdue = await TestData.AddGoalAsync(factory, userId, "Old", 50_000, deadline: stored);
        var path = $"{GoalsPath}/{overdue.Id}";

        using var keptDeadline = await SendAsync(client, HttpMethod.Put, path, Body("Old", 50_000, stored, null));
        using var moved = await SendAsync(client, HttpMethod.Put, path, Body("Old", 50_000, Current.AddMonths(-1).ToString(), null));
        using var cleared = await SendAsync(client, HttpMethod.Put, path, Body("Old", 50_000, null, null));

        keptDeadline.StatusCode.ShouldBe(HttpStatusCode.OK);
        var kept = await keptDeadline.Content.ReadFromJsonAsync<JsonElement>(Ct);
        kept.GetProperty("status").GetString().ShouldBe("overdue");
        kept.GetProperty("monthly").ValueKind.ShouldBe(JsonValueKind.Null);
        kept.GetProperty("deadline").GetString().ShouldBe(stored);
        await ShouldHaveErrorAsync(moved, "deadline", "validation.deadlinePast");
        cleared.StatusCode.ShouldBe(HttpStatusCode.OK);
        var active = await cleared.Content.ReadFromJsonAsync<JsonElement>(Ct);
        active.GetProperty("status").GetString().ShouldBe("active");
        active.GetProperty("deadline").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Should_RebaseOnUpdate()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.EUR, IsoDay(Today), 4.25m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var goal = await TestData.AddGoalAsync(isolated, userId, "Trip", 800_000, saved: 300_000);

        using (var toEuro = await PutSettingsAsync(client, "EUR"))
        {
            toEuro.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var update = await SendAsync(client, HttpMethod.Put, $"{GoalsPath}/{goal.Id}", Body("Trip", 200_000, null, null));

        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await update.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("target").GetInt64().ShouldBe(200_000);
        body.GetProperty("saved").GetInt64().ShouldBe(MinorUnits.Convert(300_000, ExchangeRate.Cross(1m, 4.25m)));
        body.GetProperty("currency").GetString().ShouldBe("EUR");

        await using var scope = isolated.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Goals.AsNoTracking().SingleAsync(stored => stored.Id == goal.Id, Ct);
        row.Currency.ShouldBe(Currency.EUR);
        row.Target.ShouldBe(200_000);
    }

    [Fact]
    public async Task Should_ValueSavingAverageAtContributionDate_When_BaseCurrencyDiffers()
    {
        var day = Today.AddDays(-10);
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.EUR, IsoDay(day), 4m), (Currency.EUR, IsoDay(Today), 5m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var goal = await TestData.AddGoalAsync(isolated, userId, "Trip", 800_000);
        await TestData.AddContributionAsync(isolated, goal, IsoDay(day), 40_000);
        await TestData.AddContributionAsync(isolated, goal, IsoDay(Today), 1_000, Currency.EUR);

        using (var toEuro = await PutSettingsAsync(client, "EUR"))
        {
            toEuro.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var list = await client.Http.GetAsync(GoalsPath, Ct);

        list.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await list.Content.ReadFromJsonAsync<JsonElement>(Ct);
        page.GetProperty("currency").GetString().ShouldBe("EUR");
        page.GetProperty("summary").GetProperty("monthly").GetInt64().ShouldBe(3_667);
        page.GetProperty("items")[0].GetProperty("target").GetInt64().ShouldBe(160_000);
    }

    [Fact]
    public async Task Should_Return404_When_GoalBelongsToAnotherUser()
    {
        using var owner = new AuthClient(factory);
        using var intruder = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        await intruder.RegisterNewAsync(Ct);
        var goal = await TestData.AddGoalAsync(factory, await UserIdAsync(owner), "Mine", 10_000);

        using var put = await SendAsync(intruder, HttpMethod.Put, $"{GoalsPath}/{goal.Id}", Body("Taken", 1, null, null));
        using var delete = await SendAsync(intruder, HttpMethod.Delete, $"{GoalsPath}/{goal.Id}", null);

        put.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        delete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var list = await owner.Http.GetAsync(GoalsPath, Ct);
        var item = (await list.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items")[0];
        item.GetProperty("name").GetString().ShouldBe("Mine");
    }

    [Fact]
    public async Task Should_Return400_When_BodyInvalidOnUnknownId()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var response = await SendAsync(client, HttpMethod.Put, $"{GoalsPath}/{Guid.NewGuid()}", Body("", 0, null, null));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_DeleteGoal()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var goal = await TestData.AddGoalAsync(factory, await UserIdAsync(client), "Bike", 10_000);
        await TestData.AddContributionAsync(factory, goal, IsoDay(Today), 1_000);

        using var first = await SendAsync(client, HttpMethod.Delete, $"{GoalsPath}/{goal.Id}", null);
        using var second = await SendAsync(client, HttpMethod.Delete, $"{GoalsPath}/{goal.Id}", null);

        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        second.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.GoalContributions.CountAsync(contribution => contribution.GoalId == goal.Id, Ct)).ShouldBe(0);
    }

    private static object Body(string name, long target, string? deadline, Guid? accountId) =>
        new { name, target, deadline, accountId };

    private static string IsoDay(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private async Task ArchiveAsync(Guid accountId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Accounts.Where(account => account.Id == accountId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(account => account.Archived, true), Ct);
    }

    private static Task<HttpResponseMessage> SendAsync(AuthClient client, HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.Http.SendAsync(client.WithXsrf(request), Ct);
    }

    private static Task<HttpResponseMessage> PutSettingsAsync(AuthClient client, string baseCurrency) =>
        SendAsync(
            client,
            HttpMethod.Put,
            "/v1/settings",
            new Dictionary<string, object?>
            {
                ["baseCurrency"] = baseCurrency,
                ["locale"] = "en",
                ["monthStartDay"] = 1,
                ["theme"] = "system",
                ["notifications"] = new { budgetLimit = false, dailyReminder = true, weeklyReport = true, goals = false }
            });

    private static async Task ShouldHaveErrorAsync(HttpResponseMessage response, string field, string code)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        errors.EnumerateObject().Select(property => property.Name).ShouldBe([field]);
        errors.GetProperty(field).EnumerateArray().Select(error => error.GetString()).ShouldBe([code]);
    }

    private static async Task<Guid> UserIdAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync("/v1/me", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }
}
