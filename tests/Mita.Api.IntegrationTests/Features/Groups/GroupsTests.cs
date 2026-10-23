using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Groups;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Groups;

public sealed class GroupsTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string GroupsPath = "/v1/groups";

    private static readonly string[] StandardKeys =
        ["groceries", "transport", "housing", "cafes", "health", "shopping", "leisure", "gifts", "other"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_Return401_When_NotAuthenticated()
    {
        using var client = new AuthClient(factory);

        using var list = await client.Http.GetAsync(GroupsPath, Ct);
        using var create = await client.Http.PostAsJsonAsync(GroupsPath, new { }, Ct);

        list.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        create.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_CreateGroup_WithMembersInOrder()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        string[] names = ["Anna", " Olya ", "Max"];

        using var response = await PostAsync(client, new { name = "  Albania 2026 ", currency = "EUR", members = names });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.EnumerateObject().Select(property => property.Name).ShouldBe(
            ["id", "name", "currency", "currencyLocked", "total", "members", "categories", "shareToken"]);
        var id = body.GetProperty("id").GetGuid();
        response.Headers.Location!.ToString().ShouldBe($"{GroupsPath}/{id}");
        body.GetProperty("name").GetString().ShouldBe("Albania 2026");
        body.GetProperty("currency").GetString().ShouldBe("EUR");
        body.GetProperty("currencyLocked").GetBoolean().ShouldBeFalse();
        body.GetProperty("total").GetInt64().ShouldBe(0);
        body.GetProperty("shareToken").ValueKind.ShouldBe(JsonValueKind.Null);

        var members = body.GetProperty("members").EnumerateArray().ToList();
        members.Select(member => member.GetProperty("name").GetString()).ShouldBe(["Anna", "Olya", "Max"]);
        members.Select(member => member.GetProperty("inUse").GetBoolean()).ShouldBe([false, false, false]);
        members.ShouldAllBe(member => member.EnumerateObject().Count() == 3);
        members.Select(member => member.GetProperty("id").GetGuid()).Distinct().Count().ShouldBe(3);

        var categories = body.GetProperty("categories").EnumerateArray().ToList();
        categories.Select(category => category.GetProperty("id").GetString()).ShouldBe(StandardKeys);
        categories.ShouldAllBe(category => category.GetProperty("standard").GetBoolean());
        categories.ShouldAllBe(category => category.GetProperty("name").ValueKind == JsonValueKind.Null);
        categories.ShouldAllBe(category => category.EnumerateObject().Count() == 3);

        using var read = await client.Http.GetAsync($"{GroupsPath}/{id}", Ct);

        read.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await read.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetRawText().ShouldBe(body.GetRawText());
    }

    [Fact]
    public async Task Should_ReportEveryInvalidField_When_CreateBodyInvalid()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        string[] names = ["Max", " max "];

        using var response = await PostAsync(client, new { name = "   ", currency = "XDR", members = names });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        errors.EnumerateObject().Select(property => property.Name).ShouldBe(
            ["name", "currency", "members"],
            ignoreOrder: true);
        errors.GetProperty("name")[0].GetString().ShouldBe("validation.required");
        errors.GetProperty("currency")[0].GetString().ShouldBe("validation.invalidValue");
        errors.GetProperty("members")[0].GetString().ShouldBe("validation.memberNameTaken");

        using var noBody = await client.Http.SendAsync(
            client.WithXsrf(new HttpRequestMessage(HttpMethod.Post, GroupsPath)),
            Ct);

        noBody.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_ListOwnGroups_NewestFirst()
    {
        using var client = new AuthClient(factory);
        using var other = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        await other.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var (first, firstMembers) = await TestData.AddGroupAsync(factory, userId, "First");
        await TestData.AddGroupExpenseAsync(factory, first, firstMembers[0], 12_000, firstMembers);
        await TestData.AddGroupExpenseAsync(factory, first, firstMembers[1], 500, firstMembers, deleted: true);
        var (second, _) = await TestData.AddGroupAsync(factory, userId, "Second", Currency.PLN);

        using var response = await client.Http.GetAsync(GroupsPath, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        page.EnumerateObject().Select(property => property.Name).ShouldBe(
            ["items", "page", "pageSize", "total"],
            ignoreOrder: true);
        page.GetProperty("page").GetInt32().ShouldBe(1);
        page.GetProperty("pageSize").GetInt32().ShouldBe(100);
        page.GetProperty("total").GetInt32().ShouldBe(2);
        var items = page.GetProperty("items").EnumerateArray().ToList();
        items.Select(item => item.GetProperty("id").GetGuid()).ShouldBe([second.Id, first.Id]);
        items.ShouldAllBe(item => item.EnumerateObject().Count() == 5);
        items.Select(item => item.GetProperty("name").GetString()).ShouldBe(["Second", "First"]);
        items.Select(item => item.GetProperty("currency").GetString()).ShouldBe(["PLN", "EUR"]);
        items.Select(item => item.GetProperty("members").GetInt32()).ShouldBe([3, 3]);
        items.Select(item => item.GetProperty("total").GetInt64()).ShouldBe([0L, 12_000L]);

        using var foreign = await other.Http.GetAsync(GroupsPath, Ct);

        foreign.StatusCode.ShouldBe(HttpStatusCode.OK);
        var foreignPage = await foreign.Content.ReadFromJsonAsync<JsonElement>(Ct);
        foreignPage.GetProperty("total").GetInt32().ShouldBe(0);
        foreignPage.GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Should_PageGroups_And_ReportInvalidPaging()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var created = new List<Guid>();
        for (var index = 0; index < 3; index++)
        {
            created.Add((await TestData.AddGroupAsync(factory, userId, $"Group {index}")).Group.Id);
        }

        using var response = await client.Http.GetAsync($"{GroupsPath}?page=2&pageSize=2", Ct);

        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        page.GetProperty("total").GetInt32().ShouldBe(3);
        page.GetProperty("page").GetInt32().ShouldBe(2);
        page.GetProperty("pageSize").GetInt32().ShouldBe(2);
        page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())
            .ShouldBe([created[0]]);

        using var tooBig = await client.Http.GetAsync($"{GroupsPath}?pageSize=101", Ct);
        using var zero = await client.Http.GetAsync($"{GroupsPath}?page=0", Ct);

        await ShouldHaveErrorAsync(tooBig, "pageSize", "validation.invalidValue");
        await ShouldHaveErrorAsync(zero, "page", "validation.invalidValue");
    }

    [Fact]
    public async Task Should_FlagMembersInUse_AndCountOnlyLiveExpenses()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var (group, members) = await TestData.AddGroupAsync(
            factory, userId, "Trip", Currency.EUR, "Payer", "Sharer", "Sender", "Receiver", "Idle", "Actor");
        await TestData.AddGroupExpenseAsync(factory, group, members[0], 1_000, [members[1]], deleted: true);
        await TestData.AddGroupExpenseAsync(factory, group, members[0], 2_000, [members[0], members[1]]);
        await TestData.AddGroupRepaymentAsync(factory, group, members[2], members[3], 300, deleted: true);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.GroupEntryEvents.Add(
                GroupEntryEvent.Create(
                    group.Id,
                    GroupEntryKind.Expense,
                    Guid.NewGuid(),
                    GroupEntryAction.Updated,
                    DateTimeOffset.UtcNow,
                    GroupActor.Member(members[5].Id),
                    "{}"));
            await db.SaveChangesAsync(Ct);
        }

        using var response = await client.Http.GetAsync($"{GroupsPath}/{group.Id}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("total").GetInt64().ShouldBe(2_000);
        body.GetProperty("currencyLocked").GetBoolean().ShouldBeTrue();
        body.GetProperty("members").EnumerateArray()
            .Select(member => (member.GetProperty("name").GetString(), member.GetProperty("inUse").GetBoolean()))
            .ShouldBe(
                [
                    ("Payer", true), ("Sharer", true), ("Sender", true), ("Receiver", true), ("Idle", false),
                    ("Actor", true)
                ]);
    }

    [Fact]
    public async Task Should_Return404_When_GroupUnknownOrForeign()
    {
        using var owner = new AuthClient(factory);
        using var intruder = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        await intruder.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(owner), "Trip");
        await TestData.AddGroupExpenseAsync(factory, group, members[0], 3_000, members);
        var unknown = Guid.NewGuid();
        var valid = new { name = "Hijacked", currency = "PLN" };

        using var unknownGet = await intruder.Http.GetAsync($"{GroupsPath}/{unknown}", Ct);
        using var unknownPut = await PutAsync(owner, unknown.ToString(), valid);
        using var unknownDelete = await DeleteAsync(owner, $"{GroupsPath}/{unknown}");
        using var foreignGet = await intruder.Http.GetAsync($"{GroupsPath}/{group.Id}", Ct);
        using var foreignPut = await PutAsync(intruder, group.Id.ToString(), valid);
        using var foreignDelete = await DeleteAsync(intruder, $"{GroupsPath}/{group.Id}");
        using var notAGuid = await owner.Http.GetAsync($"{GroupsPath}/not-a-guid", Ct);
        using var invalidBody = await PutAsync(owner, unknown.ToString(), new { name = " ", currency = "PLN" });

        unknownGet.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        unknownPut.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        unknownDelete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        foreignGet.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        foreignPut.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        foreignDelete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        notAGuid.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ShouldHaveErrorAsync(invalidBody, "name", "validation.required");

        using var stored = await owner.Http.GetAsync($"{GroupsPath}/{group.Id}", Ct);
        stored.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await stored.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("name").GetString().ShouldBe("Trip");
        body.GetProperty("currency").GetString().ShouldBe("EUR");
        body.GetProperty("total").GetInt64().ShouldBe(3_000);
    }

    [Fact]
    public async Task Should_UpdateGroup_And_LockCurrency_When_EntriesExist()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var (created, _) = await TestData.AddGroupAsync(factory, userId, "Trip");

        using var open = await PutAsync(client, created.Id.ToString(), new { name = "  Albania ", currency = "PLN" });

        open.StatusCode.ShouldBe(HttpStatusCode.OK);
        var renamed = await open.Content.ReadFromJsonAsync<JsonElement>(Ct);
        renamed.GetProperty("id").GetGuid().ShouldBe(created.Id);
        renamed.GetProperty("name").GetString().ShouldBe("Albania");
        renamed.GetProperty("currency").GetString().ShouldBe("PLN");
        renamed.GetProperty("currencyLocked").GetBoolean().ShouldBeFalse();

        var group = await StoredGroupAsync(created.Id);
        var members = await StoredMembersAsync(created.Id);
        await TestData.AddGroupExpenseAsync(factory, group, members[0], 5_000, members);

        using var locked = await PutAsync(client, created.Id.ToString(), new { name = "Albania", currency = "EUR" });
        using var sameCurrency = await PutAsync(client, created.Id.ToString(), new { name = "Albania 2026", currency = "PLN" });

        await ShouldHaveErrorAsync(locked, "currency", "validation.groupCurrencyLocked");
        sameCurrency.StatusCode.ShouldBe(HttpStatusCode.OK);
        var kept = await sameCurrency.Content.ReadFromJsonAsync<JsonElement>(Ct);
        kept.GetProperty("name").GetString().ShouldBe("Albania 2026");
        kept.GetProperty("currency").GetString().ShouldBe("PLN");
        kept.GetProperty("currencyLocked").GetBoolean().ShouldBeTrue();
        kept.GetProperty("total").GetInt64().ShouldBe(5_000);
        (await StoredGroupAsync(created.Id)).Name.ShouldBe("Albania 2026");

        var (deletedOnly, deletedMembers) = await TestData.AddGroupAsync(factory, userId, "Deleted only");
        await TestData.AddGroupExpenseAsync(
            factory, deletedOnly, deletedMembers[0], 700, deletedMembers, deleted: true);
        var (repaymentOnly, repaymentMembers) = await TestData.AddGroupAsync(factory, userId, "Repayment only");
        await TestData.AddGroupRepaymentAsync(factory, repaymentOnly, repaymentMembers[0], repaymentMembers[1], 400);

        foreach (var lockedGroup in new[] { deletedOnly, repaymentOnly })
        {
            using var change = await PutAsync(client, lockedGroup.Id.ToString(), new { name = "Renamed", currency = "PLN" });
            using var rename = await PutAsync(client, lockedGroup.Id.ToString(), new { name = "Renamed", currency = "EUR" });

            await ShouldHaveErrorAsync(change, "currency", "validation.groupCurrencyLocked");
            rename.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await StoredGroupAsync(lockedGroup.Id)).Currency.ShouldBe(Currency.EUR);
        }
    }

    [Fact]
    public async Task Should_DeleteGroup_WithEverything()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var (group, members) = await TestData.AddGroupAsync(factory, userId, "Trip");
        var (survivor, _) = await TestData.AddGroupAsync(factory, userId, "Survivor");
        await TestData.AddGroupExpenseAsync(factory, group, members[0], 9_000, members);
        await TestData.AddGroupExpenseAsync(factory, group, members[1], 600, [members[2]], deleted: true);
        await TestData.AddGroupRepaymentAsync(factory, group, members[2], members[0], 1_500);
        await AddCategorisedExpenseAsync(group, members[1]);

        (await RowCountsAsync(group.Id)).ShouldBe([1, 3, 1, 3, 7, 1, 5]);

        using var response = await DeleteAsync(client, $"{GroupsPath}/{group.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var read = await client.Http.GetAsync($"{GroupsPath}/{group.Id}", Ct);
        read.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await RowCountsAsync(group.Id)).ShouldBe([0, 0, 0, 0, 0, 0, 0]);
        using var list = await client.Http.GetAsync(GroupsPath, Ct);
        (await list.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid()).ShouldBe([survivor.Id]);
    }

    private async Task AddCategorisedExpenseAsync(Group group, GroupMember payer)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var category = GroupCategory.Create(group.Id, "Beach");
        var members = await db.GroupMembers.Where(member => member.GroupId == group.Id).ToListAsync(Ct);
        var shares = GroupExpenseSplits.Equal(
            800,
            800,
            members.Select(member => member.Id).ToList(),
            payer.Id);
        var expense = GroupExpense.Create(
            group.Id,
            new ExpenseDetails(
                payer.Id, 800, group.Currency, new DateOnly(2026, 10, 2), null, category.Id, [], null, 1m, false, 800,
                GroupSplitMode.Equal, shares));
        db.GroupCategories.Add(category);
        db.GroupExpenses.Add(expense);
        db.GroupEntryEvents.Add(
            GroupEntryEvent.Create(
                group.Id,
                GroupEntryKind.Expense,
                expense.Id,
                GroupEntryAction.Created,
                DateTimeOffset.UtcNow,
                GroupActor.Member(payer.Id),
                "{}"));
        await db.SaveChangesAsync(Ct);
    }

    private async Task<int[]> RowCountsAsync(Guid groupId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return
        [
            await db.Groups.CountAsync(row => row.Id == groupId, Ct),
            await db.GroupMembers.CountAsync(row => row.GroupId == groupId, Ct),
            await db.GroupCategories.CountAsync(row => row.GroupId == groupId, Ct),
            await db.GroupExpenses.CountAsync(row => row.GroupId == groupId, Ct),
            await db.GroupExpenseShares.CountAsync(row => row.GroupId == groupId, Ct),
            await db.GroupRepayments.CountAsync(row => row.GroupId == groupId, Ct),
            await db.GroupEntryEvents.CountAsync(row => row.GroupId == groupId, Ct)
        ];
    }

    private async Task<Group> StoredGroupAsync(Guid groupId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Groups.AsNoTracking().SingleAsync(row => row.Id == groupId, Ct);
    }

    private async Task<GroupMember[]> StoredMembersAsync(Guid groupId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.GroupMembers.AsNoTracking()
            .Where(row => row.GroupId == groupId)
            .OrderBy(row => row.Position)
            .ToArrayAsync(Ct);
    }

    private static Task<HttpResponseMessage> PostAsync(AuthClient client, object body) =>
        client.Http.SendAsync(
            client.WithXsrf(new HttpRequestMessage(HttpMethod.Post, GroupsPath) { Content = JsonContent.Create(body) }),
            Ct);

    private static Task<HttpResponseMessage> PutAsync(AuthClient client, string id, object body) =>
        client.Http.SendAsync(
            client.WithXsrf(
                new HttpRequestMessage(HttpMethod.Put, $"{GroupsPath}/{id}") { Content = JsonContent.Create(body) }),
            Ct);

    private static Task<HttpResponseMessage> DeleteAsync(AuthClient client, string path) =>
        client.Http.SendAsync(client.WithXsrf(new HttpRequestMessage(HttpMethod.Delete, path)), Ct);

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
