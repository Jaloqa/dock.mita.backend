using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Debts;
using Mita.Domain.Groups;
using Mita.Domain.Money;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Debts;

public sealed class GroupDebtsTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string GroupDebtsPath = "/v1/debts/groups";
    private const string DebtsPath = "/v1/debts";
    private const string OverviewPath = "/v1/overview";

    private static readonly string[] LineProperties = ["groupId", "name", "currency", "balance"];

    private static readonly string[] SummaryProperties = ["owedToMe", "owedByMe", "open", "overdue", "groups"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Should_ListGroupBalances()
    {
        await using var isolated = factory.WithIsolatedData();
        await SeedRatesAsync(isolated);
        using var ownerA = new AuthClient(isolated);
        using var linkedB = new AuthClient(isolated);
        using var ownerC = new AuthClient(isolated);
        var world = await SeedGroupsAsync(isolated, ownerA, linkedB, ownerC);

        var page = await GetJsonAsync(linkedB, GroupDebtsPath);

        page.GetProperty("page").GetInt32().ShouldBe(1);
        page.GetProperty("pageSize").GetInt32().ShouldBe(100);
        page.GetProperty("total").GetInt32().ShouldBe(3);
        Lines(page).ShouldBe(
        [
            (world.Done.Id, "Done", "USD", 0L),
            (world.Home.Id, "Home", "PLN", -15_000L),
            (world.Trip.Id, "Trip", "EUR", 6_000L)
        ]);
        foreach (var line in page.GetProperty("items").EnumerateArray())
        {
            line.EnumerateObject().Select(property => property.Name).ShouldBe(LineProperties);
        }

        var second = await GetJsonAsync(linkedB, $"{GroupDebtsPath}?page=2&pageSize=2");

        second.GetProperty("total").GetInt32().ShouldBe(3);
        Lines(second).ShouldBe([(world.Trip.Id, "Trip", "EUR", 6_000L)]);
    }

    [Fact]
    public async Task Should_IncludeGroupBalances_InSummary()
    {
        await using var isolated = factory.WithIsolatedData();
        await SeedRatesAsync(isolated);
        using var ownerA = new AuthClient(isolated);
        using var linkedB = new AuthClient(isolated);
        using var ownerC = new AuthClient(isolated);
        var world = await SeedGroupsAsync(isolated, ownerA, linkedB, ownerC);
        await TestData.AddDebtAsync(isolated, world.LinkedB, DebtDirection.Lent, 10_000, date: IsoDay(Today));

        var summary = (await GetJsonAsync(linkedB, DebtsPath)).GetProperty("summary");
        var overview = await GetJsonAsync(linkedB, OverviewPath);

        summary.EnumerateObject().Select(property => property.Name).ShouldBe(SummaryProperties);
        Summary(summary).ShouldBe((35_800L, 15_000L, 1, 0, 2));
        overview.GetProperty("debts").GetRawText().ShouldBe(summary.GetRawText());
    }

    [Fact]
    public async Task Should_IgnoreGroups_WhereUserIsNotLinked()
    {
        await using var isolated = factory.WithIsolatedData();
        await SeedRatesAsync(isolated);
        using var ownerA = new AuthClient(isolated);
        using var linkedB = new AuthClient(isolated);
        var ownerId = await RegisterAsync(ownerA);
        var linkedId = await RegisterAsync(linkedB);
        var (trip, members) = await TestData.AddGroupAsync(isolated, ownerId, "Trip", Currency.EUR, "Anna", "Olya", "Max");
        await TestData.AddGroupExpenseAsync(isolated, trip, members[1], 9_000, members);
        await TestData.LinkMemberAsync(isolated, members[1], linkedId);

        var unlinkedLines = await GetJsonAsync(ownerA, GroupDebtsPath);
        var unlinkedSummary = (await GetJsonAsync(ownerA, DebtsPath)).GetProperty("summary");
        var unlinkedOverview = await GetJsonAsync(ownerA, OverviewPath);

        unlinkedLines.GetProperty("total").GetInt32().ShouldBe(0);
        Lines(unlinkedLines).ShouldBeEmpty();
        Summary(unlinkedSummary).ShouldBe((0L, 0L, 0, 0, 0));
        Summary(unlinkedOverview.GetProperty("debts")).ShouldBe((0L, 0L, 0, 0, 0));
        Summary((await GetJsonAsync(linkedB, DebtsPath)).GetProperty("summary")).ShouldBe((25_800L, 0L, 0, 0, 1));

        await TestData.LinkMemberAsync(isolated, members[0], ownerId);

        Lines(await GetJsonAsync(ownerA, GroupDebtsPath)).ShouldBe([(trip.Id, "Trip", "EUR", -3_000L)]);
        Summary((await GetJsonAsync(ownerA, DebtsPath)).GetProperty("summary")).ShouldBe((0L, 12_900L, 0, 0, 1));
        Summary((await GetJsonAsync(ownerA, OverviewPath)).GetProperty("debts")).ShouldBe((0L, 12_900L, 0, 0, 1));
    }

    [Fact]
    public async Task Should_Return503_When_GroupCurrencyHasNoRate()
    {
        await using var isolated = factory.WithIsolatedData();
        await SeedRatesAsync(isolated);
        using var owner = new AuthClient(isolated);
        using var linked = new AuthClient(isolated);
        var ownerId = await RegisterAsync(owner);
        var linkedId = await RegisterAsync(linked);
        var (kuwait, members) = await TestData.AddGroupAsync(isolated, ownerId, "Kuwait", Currency.KWD, "Ali", "Bea");
        await TestData.LinkMemberAsync(isolated, members[1], linkedId);

        Summary((await GetJsonAsync(linked, DebtsPath)).GetProperty("summary")).ShouldBe((0L, 0L, 0, 0, 0));

        await TestData.AddGroupExpenseAsync(isolated, kuwait, members[0], 5_000, members);

        using var debts = await linked.Http.GetAsync(DebtsPath, Ct);
        using var overview = await linked.Http.GetAsync(OverviewPath, Ct);
        var lines = await GetJsonAsync(linked, GroupDebtsPath);

        await ShouldBeUnavailableAsync(debts);
        await ShouldBeUnavailableAsync(overview);
        Lines(lines).ShouldBe([(kuwait.Id, "Kuwait", "KWD", -2_500L)]);
    }

    [Fact]
    public async Task Should_RefuseRequest_When_PagingInvalidOrAnonymous()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        using var anonymous = new AuthClient(factory);

        using var tooLarge = await client.Http.GetAsync($"{GroupDebtsPath}?pageSize=101", Ct);
        using var zeroPage = await client.Http.GetAsync($"{GroupDebtsPath}?page=0", Ct);
        using var unauthorized = await anonymous.Http.GetAsync(GroupDebtsPath, Ct);

        await ShouldHaveErrorAsync(tooLarge, "pageSize", "validation.invalidValue");
        await ShouldHaveErrorAsync(zeroPage, "page", "validation.invalidValue");
        unauthorized.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static async Task<World> SeedGroupsAsync(
        WebApplicationFactory<Program> app,
        AuthClient ownerA,
        AuthClient linkedB,
        AuthClient ownerC)
    {
        var ownerAId = await RegisterAsync(ownerA);
        var linkedBId = await RegisterAsync(linkedB);
        var ownerCId = await RegisterAsync(ownerC);

        var (trip, tripMembers) = await TestData.AddGroupAsync(app, ownerAId, "Trip", Currency.EUR, "Anna", "Olya", "Max");
        await TestData.LinkMemberAsync(app, tripMembers[1], linkedBId);
        await TestData.AddGroupExpenseAsync(app, trip, tripMembers[1], 9_000, tripMembers);
        await TestData.AddGroupExpenseAsync(app, trip, tripMembers[0], 5_000, tripMembers, deleted: true);

        var (home, homeMembers) = await TestData.AddGroupAsync(app, ownerCId, "Home", Currency.PLN, "Cleo", "Kasia");
        await TestData.LinkMemberAsync(app, homeMembers[1], linkedBId);
        await TestData.AddGroupExpenseAsync(app, home, homeMembers[0], 30_000, homeMembers);

        var (done, doneMembers) = await TestData.AddGroupAsync(app, ownerCId, "Done", Currency.USD, "Bea", "Zed");
        await TestData.LinkMemberAsync(app, doneMembers[0], linkedBId);
        await TestData.AddGroupExpenseAsync(app, done, doneMembers[0], 2_000, doneMembers);
        await TestData.AddGroupRepaymentAsync(app, done, doneMembers[1], doneMembers[0], 1_000);

        return new World(linkedBId, trip, home, done);
    }

    private static Task SeedRatesAsync(WebApplicationFactory<Program> app) =>
        TestData.AddRatesAsync(app, (Currency.EUR, IsoDay(Today), 4.30m));

    private static List<(Guid GroupId, string? Name, string? Currency, long Balance)> Lines(JsonElement page) =>
        page.GetProperty("items").EnumerateArray()
            .Select(line => (
                line.GetProperty("groupId").GetGuid(),
                line.GetProperty("name").GetString(),
                line.GetProperty("currency").GetString(),
                line.GetProperty("balance").GetInt64()))
            .ToList();

    private static (long OwedToMe, long OwedByMe, int Open, int Overdue, int Groups) Summary(JsonElement summary) =>
        (
            summary.GetProperty("owedToMe").GetInt64(),
            summary.GetProperty("owedByMe").GetInt64(),
            summary.GetProperty("open").GetInt32(),
            summary.GetProperty("overdue").GetInt32(),
            summary.GetProperty("groups").GetInt32());

    private static async Task ShouldBeUnavailableAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString()
            .ShouldBe("rates.unavailable");
    }

    private static async Task ShouldHaveErrorAsync(HttpResponseMessage response, string field, string code)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        errors.EnumerateObject().Select(property => property.Name).ShouldBe([field]);
        errors.GetProperty(field).EnumerateArray().Select(error => error.GetString()).ShouldBe([code]);
    }

    private static async Task<JsonElement> GetJsonAsync(AuthClient client, string path)
    {
        using var response = await client.Http.GetAsync(path, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, path);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task<Guid> RegisterAsync(AuthClient client)
    {
        await client.RegisterNewAsync(Ct);
        return (await GetJsonAsync(client, "/v1/me")).GetProperty("id").GetGuid();
    }

    private static string IsoDay(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private sealed record World(Guid LinkedB, Group Trip, Group Home, Group Done);
}
