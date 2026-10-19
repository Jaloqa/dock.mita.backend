using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Accounts;
using Mita.Domain.Money;
using Mita.Domain.Periods;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Goals;

public sealed class ContributionsTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string GoalsPath = "/v1/goals";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Should_AddUnlinkedContribution_WithoutMovingMoney()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var card = await TestData.AddAccountAsync(factory, userId, openingBalance: 500_000);
        var goal = await TestData.AddGoalAsync(
            factory, userId, "Wakacje", 800_000, deadline: PeriodKey.Of(Today).AddMonths(4).ToString());

        using var response = await ContributeAsync(client, goal.Id, Body(120_000, Today, card.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Location.ShouldBeNull();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("id").GetGuid().ShouldBe(goal.Id);
        body.GetProperty("saved").GetInt64().ShouldBe(120_000);
        body.GetProperty("remaining").GetInt64().ShouldBe(680_000);
        body.GetProperty("percent").GetInt32().ShouldBe(15);
        body.GetProperty("monthly").GetInt64().ShouldBe(170_000);
        body.GetProperty("status").GetString().ShouldBe("active");
        body.GetProperty("currency").GetString().ShouldBe("PLN");
        (await BalancesAsync(client))[card.Id].ShouldBe(500_000);
        (await TransferCountAsync(factory, userId)).ShouldBe(0);
        var stored = await ContributionsAsync(factory, goal.Id);
        stored.Single().FromAccountId.ShouldBe(card.Id);
        stored.Single().TransferId.ShouldBeNull();

        using var withoutSource = await ContributeAsync(client, goal.Id, Body(1_000, Today, null));

        withoutSource.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await withoutSource.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("saved").GetInt64().ShouldBe(121_000);
        (await TransferCountAsync(factory, userId)).ShouldBe(0);
    }

    [Fact]
    public async Task Should_ValidateContributionAccounts()
    {
        using var client = new AuthClient(factory);
        using var other = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        await other.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var card = await TestData.AddAccountAsync(factory, userId);
        var archivedCard = await TestData.AddAccountAsync(factory, userId, archived: true);
        var foreign = await TestData.AddAccountAsync(factory, await UserIdAsync(other));
        var savings = await TestData.AddAccountAsync(factory, userId, type: AccountType.Savings);
        var archivedSavings = await TestData.AddAccountAsync(factory, userId, type: AccountType.Savings, archived: true);
        var unlinked = await TestData.AddGoalAsync(factory, userId, "Free", 100_000);
        var linked = await TestData.AddGoalAsync(factory, userId, "Linked", 100_000, accountId: savings.Id);
        var linkedToArchived = await TestData.AddGoalAsync(factory, userId, "Old", 100_000, accountId: archivedSavings.Id);

        using var foreignSource = await ContributeAsync(client, unlinked.Id, Body(1_000, Today, foreign.Id));
        using var missingSource = await ContributeAsync(client, linked.Id, Body(1_000, Today, null));
        using var sameAccount = await ContributeAsync(client, linked.Id, Body(1_000, Today, savings.Id));
        using var archivedLink = await ContributeAsync(client, linkedToArchived.Id, Body(1_000, Today, card.Id));
        using var archivedSource = await ContributeAsync(client, linked.Id, Body(1_000, Today, archivedCard.Id));
        using var unknownGoal = await ContributeAsync(client, Guid.NewGuid(), Body(1_000, Today, card.Id));
        using var unknownGoalEmpty = await ContributeAsync(client, Guid.NewGuid(), new { });
        using var foreignGoal = await ContributeAsync(other, linked.Id, Body(1_000, Today, null));

        await ShouldHaveErrorAsync(foreignSource, "fromAccountId", "validation.unknownAccount");
        await ShouldHaveErrorAsync(missingSource, "fromAccountId", "validation.required");
        await ShouldHaveErrorAsync(sameAccount, "fromAccountId", "validation.sameAccount");
        await ShouldHaveErrorAsync(archivedLink, "accountId", "validation.archivedAccount");
        await ShouldHaveErrorAsync(archivedSource, "fromAccountId", "validation.archivedAccount");
        unknownGoal.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        unknownGoalEmpty.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        foreignGoal.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await TransferCountAsync(factory, userId)).ShouldBe(0);
    }

    [Fact]
    public async Task Should_TransferIntoLinkedAccount()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var card = await TestData.AddAccountAsync(factory, userId, openingBalance: 200_000);
        var savings = await TestData.AddAccountAsync(factory, userId, type: AccountType.Savings);
        var goal = await TestData.AddGoalAsync(factory, userId, "Mieszkanie", 2_500_000, accountId: savings.Id);

        using var response = await ContributeAsync(client, goal.Id, Body(50_000, Today, card.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("saved").GetInt64().ShouldBe(50_000);
        body.GetProperty("percent").GetInt32().ShouldBe(2);
        body.GetProperty("accountId").GetGuid().ShouldBe(savings.Id);
        var balances = await BalancesAsync(client);
        balances[card.Id].ShouldBe(150_000);
        balances[savings.Id].ShouldBe(50_000);
        var transfer = await NewestTransferAsync(factory, userId);
        transfer.FromAccountId.ShouldBe(card.Id);
        transfer.ToAccountId.ShouldBe(savings.Id);
        transfer.FromAmount.ShouldBe(50_000);
        transfer.ToAmount.ShouldBe(50_000);
        transfer.Rate.ShouldBe(1m);
        transfer.Note.ShouldBe("Mieszkanie");
        transfer.Date.ShouldBe(Today);
        var contribution = (await ContributionsAsync(factory, goal.Id)).Single();
        contribution.TransferId.ShouldBe(transfer.Id);
        contribution.FromAccountId.ShouldBe(card.Id);
        contribution.BaseAmount.ShouldBe(50_000);
    }

    [Fact]
    public async Task Should_ConvertCrossCurrencyContribution()
    {
        var day = Today.AddDays(-3);
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.EUR, IsoDay(day), 4.2717m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var euroAccount = await TestData.AddAccountAsync(isolated, userId, Currency.EUR, openingBalance: 100_000);
        var plnSavings = await TestData.AddAccountAsync(isolated, userId, type: AccountType.Savings);
        var goal = await TestData.AddGoalAsync(isolated, userId, "Dom", 10_000_000, accountId: plnSavings.Id);

        using var stored = await ContributeAsync(client, goal.Id, Body(10_000, day, euroAccount.Id));

        stored.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await stored.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("saved").GetInt64().ShouldBe(42_717);

        using var manual = await ContributeAsync(client, goal.Id, Body(10_000, day, euroAccount.Id, 4));

        manual.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await manual.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("saved").GetInt64().ShouldBe(42_717 + 40_000);
        var manualTransfer = await NewestTransferAsync(isolated, userId);
        manualTransfer.ToAmount.ShouldBe(40_000);
        manualTransfer.Rate.ShouldBe(4m);
        manualTransfer.Date.ShouldBe(day);

        var plnCard = await TestData.AddAccountAsync(isolated, userId, openingBalance: 100_000);
        var euroSavings = await TestData.AddAccountAsync(isolated, userId, Currency.EUR, type: AccountType.Savings);
        var euroGoal = await TestData.AddGoalAsync(isolated, userId, "Euro", 10_000_000, accountId: euroSavings.Id);

        using var reverse = await ContributeAsync(client, euroGoal.Id, Body(43_000, day, plnCard.Id, 0.25));

        reverse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await reverse.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("saved").GetInt64().ShouldBe(45_921);
        var balances = await BalancesAsync(client);
        balances[euroSavings.Id].ShouldBe(10_750);
        balances[plnCard.Id].ShouldBe(57_000);
        balances[euroAccount.Id].ShouldBe(80_000);
        balances[plnSavings.Id].ShouldBe(82_717);
    }

    [Fact]
    public async Task Should_Return503AndWriteNothing_When_RateMissing()
    {
        await using var isolated = factory.WithIsolatedData();
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var euroAccount = await TestData.AddAccountAsync(isolated, userId, Currency.EUR, openingBalance: 100_000);
        var plnSavings = await TestData.AddAccountAsync(isolated, userId, type: AccountType.Savings);
        var goal = await TestData.AddGoalAsync(isolated, userId, "Dom", 100_000, saved: 5_000, accountId: plnSavings.Id);

        using var response = await ContributeAsync(client, goal.Id, Body(10_000, Today, euroAccount.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString().ShouldBe("rates.unavailable");
        (await TransferCountAsync(isolated, userId)).ShouldBe(0);
        (await ContributionsAsync(isolated, goal.Id)).ShouldBeEmpty();
        await using var scope = isolated.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Goals.AsNoTracking().SingleAsync(row => row.Id == goal.Id, Ct)).Saved.ShouldBe(5_000);
    }

    [Fact]
    public async Task Should_PassTarget_And_ReportReached()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var goal = await TestData.AddGoalAsync(
            factory, userId, "Rower", 10_000, deadline: PeriodKey.Of(Today).AddMonths(2).ToString());

        using var response = await ContributeAsync(client, goal.Id, Body(15_000, Today, null));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("saved").GetInt64().ShouldBe(15_000);
        body.GetProperty("remaining").GetInt64().ShouldBe(0);
        body.GetProperty("percent").GetInt32().ShouldBe(150);
        body.GetProperty("status").GetString().ShouldBe("reached");
        body.GetProperty("monthly").ValueKind.ShouldBe(JsonValueKind.Null);
        var page = await ListAsync(client);
        page.GetProperty("summary").GetProperty("active").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Should_AverageLastThreeMonths()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var card = await TestData.AddAccountAsync(factory, userId, openingBalance: 1_000_000);
        var savings = await TestData.AddAccountAsync(factory, userId, type: AccountType.Savings);
        var free = await TestData.AddGoalAsync(factory, userId, "Free", 1_000_000);
        var linked = await TestData.AddGoalAsync(factory, userId, "Linked", 1_000_000, accountId: savings.Id);

        using var today = await ContributeAsync(client, free.Id, Body(30_000, Today, null));
        using var fortyDaysAgo = await ContributeAsync(client, linked.Id, Body(60_000, Today.AddDays(-40), card.Id));
        using var threeMonthsAgo = await ContributeAsync(client, free.Id, Body(90_000, Today.AddMonths(-3), null));
        using var inFiveDays = await ContributeAsync(client, free.Id, Body(12_000, Today.AddDays(5), null));

        today.StatusCode.ShouldBe(HttpStatusCode.OK);
        fortyDaysAgo.StatusCode.ShouldBe(HttpStatusCode.OK);
        threeMonthsAgo.StatusCode.ShouldBe(HttpStatusCode.OK);
        inFiveDays.StatusCode.ShouldBe(HttpStatusCode.OK);
        var summary = (await ListAsync(client)).GetProperty("summary");
        summary.GetProperty("saved").GetInt64().ShouldBe(192_000);
        summary.GetProperty("monthly").GetInt64().ShouldBe(30_000);
        summary.GetProperty("active").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task Should_KeepTransfers_When_GoalDeleted()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var card = await TestData.AddAccountAsync(factory, userId, openingBalance: 100_000);
        var savings = await TestData.AddAccountAsync(factory, userId, type: AccountType.Savings);
        var goal = await TestData.AddGoalAsync(factory, userId, "Auto", 1_000_000, accountId: savings.Id);
        using (var contribution = await ContributeAsync(client, goal.Id, Body(30_000, Today, card.Id)))
        {
            contribution.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var delete = await SendAsync(client, HttpMethod.Delete, $"{GoalsPath}/{goal.Id}", null);

        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await TransferCountAsync(factory, userId)).ShouldBe(1);
        var balances = await BalancesAsync(client);
        balances[card.Id].ShouldBe(70_000);
        balances[savings.Id].ShouldBe(30_000);
    }

    private static Dictionary<string, object?> Body(long amount, DateOnly date, Guid? fromAccountId, double? rate = null)
    {
        var body = new Dictionary<string, object?> { ["amount"] = amount, ["date"] = IsoDay(date) };
        if (fromAccountId is not null)
        {
            body["fromAccountId"] = fromAccountId;
        }

        if (rate is not null)
        {
            body["rate"] = rate;
        }

        return body;
    }

    private static string IsoDay(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static Task<HttpResponseMessage> ContributeAsync(AuthClient client, Guid goalId, object body) =>
        SendAsync(client, HttpMethod.Post, $"{GoalsPath}/{goalId}/contributions", body);

    private static Task<HttpResponseMessage> SendAsync(AuthClient client, HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.Http.SendAsync(client.WithXsrf(request), Ct);
    }

    private static async Task<JsonElement> ListAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync(GoalsPath, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task<Dictionary<Guid, long>> BalancesAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync("/v1/accounts", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return page.GetProperty("items").EnumerateArray().ToDictionary(
            item => item.GetProperty("id").GetGuid(),
            item => item.GetProperty("balance").GetInt64());
    }

    private static async Task<int> TransferCountAsync(WebApplicationFactory<Program> app, Guid userId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Transfers.CountAsync(transfer => transfer.UserId == userId, Ct);
    }

    private static async Task<Transfer> NewestTransferAsync(WebApplicationFactory<Program> app, Guid userId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Transfers.AsNoTracking()
            .Where(transfer => transfer.UserId == userId)
            .OrderByDescending(transfer => transfer.CreatedAt)
            .ThenByDescending(transfer => transfer.Id)
            .FirstAsync(Ct);
    }

    private static async Task<List<Mita.Domain.Goals.GoalContribution>> ContributionsAsync(
        WebApplicationFactory<Program> app,
        Guid goalId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.GoalContributions.AsNoTracking().Where(row => row.GoalId == goalId).ToListAsync(Ct);
    }

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
