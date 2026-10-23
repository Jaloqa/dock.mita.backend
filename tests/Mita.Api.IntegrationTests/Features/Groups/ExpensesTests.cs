using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Groups;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Groups;

public sealed class ExpensesTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string GroupsPath = "/v1/groups";

    private static readonly string[] ExpenseProperties =
    [
        "id", "payerId", "amount", "currency", "date", "categoryId", "tags", "description", "rate", "rateManual",
        "groupAmount", "split", "shares", "deleted", "createdBy", "createdAt", "updatedBy", "updatedAt"
    ];

    private static readonly string[] StateProperties =
    [
        "payerId", "amount", "currency", "date", "categoryId", "tags", "description", "rate", "rateManual", "groupAmount",
        "split", "shares"
    ];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Should_AddExpense_SplitEquallyByDefault()
    {
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var (group, members) = await TestData.AddGroupAsync(factory, userId);
        var (anna, olya, max) = (members[0], members[1], members[2]);

        using var response = await PostAsync(
            client,
            group.Id,
            new { payerId = olya.Id, amount = 10_000, currency = "EUR", date = IsoDay(Today) });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldBeNull();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.EnumerateObject().Select(property => property.Name).ShouldBe(ExpenseProperties, ignoreOrder: true);
        body.GetProperty("payerId").GetGuid().ShouldBe(olya.Id);
        body.GetProperty("amount").GetInt64().ShouldBe(10_000);
        body.GetProperty("currency").GetString().ShouldBe("EUR");
        body.GetProperty("date").GetString().ShouldBe(IsoDay(Today));
        body.GetProperty("categoryId").GetString().ShouldBe("other");
        body.GetProperty("tags").GetArrayLength().ShouldBe(0);
        body.GetProperty("description").ValueKind.ShouldBe(JsonValueKind.Null);
        body.GetProperty("rate").GetDouble().ShouldBe(1);
        body.GetProperty("rateManual").GetBoolean().ShouldBeFalse();
        body.GetProperty("groupAmount").GetInt64().ShouldBe(10_000);
        body.GetProperty("split").GetString().ShouldBe("equal");
        body.GetProperty("shares").EnumerateArray()
            .ShouldAllBe(share => share.EnumerateObject().Count() == 3);
        Shares(body).ShouldBe([(anna.Id, 3_333L, 3_333L), (olya.Id, 3_334L, 3_334L), (max.Id, 3_333L, 3_333L)]);
        body.GetProperty("deleted").GetBoolean().ShouldBeFalse();
        var createdBy = body.GetProperty("createdBy");
        createdBy.EnumerateObject().Select(property => property.Name).ShouldBe(["kind", "memberId", "name"], ignoreOrder: true);
        createdBy.GetProperty("kind").GetString().ShouldBe("owner");
        createdBy.GetProperty("memberId").ValueKind.ShouldBe(JsonValueKind.Null);
        createdBy.GetProperty("name").GetString().ShouldBe(credentials.Name);
        body.GetProperty("createdAt").GetString()!.ShouldMatch(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$");
        body.GetProperty("updatedBy").ValueKind.ShouldBe(JsonValueKind.Null);
        body.GetProperty("updatedAt").ValueKind.ShouldBe(JsonValueKind.Null);

        await ShouldHaveRecordedCreationAsync(factory, userId, body);
    }

    [Fact]
    public async Task Should_ConvertExpense_AtTheDateRate()
    {
        await using var isolated = factory.WithIsolatedData();
        var date = IsoDay(Today.AddDays(-3));
        await TestData.AddRatesAsync(isolated, (Currency.ALL, date, 0.0440m), (Currency.EUR, date, 4.30m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var (group, members) = await TestData.AddGroupAsync(isolated, userId);
        using var added = await SendAsync(client, HttpMethod.Post, $"{GroupsPath}/{group.Id}/members", new { name = "Kasia" });
        added.StatusCode.ShouldBe(HttpStatusCode.Created);
        var kasia = (await added.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
        var (anna, olya, max) = (members[0].Id, members[1].Id, members[2].Id);

        using var stored = await PostAsync(
            client,
            group.Id,
            new { payerId = olya, amount = 300_000, currency = "ALL", date });

        stored.StatusCode.ShouldBe(HttpStatusCode.Created);
        var storedBody = await stored.Content.ReadFromJsonAsync<JsonElement>(Ct);
        storedBody.GetProperty("currency").GetString().ShouldBe("ALL");
        storedBody.GetProperty("rate").GetDouble().ShouldBe(0.0102326);
        storedBody.GetProperty("rateManual").GetBoolean().ShouldBeFalse();
        storedBody.GetProperty("groupAmount").GetInt64().ShouldBe(3_070);
        Shares(storedBody).ShouldBe(
            [(anna, 75_000L, 768L), (olya, 75_000L, 768L), (max, 75_000L, 767L), (kasia, 75_000L, 767L)]);
        await ShouldHaveRecordedCreationAsync(isolated, userId, storedBody);

        using var manual = await PostAsync(
            client,
            group.Id,
            new { payerId = olya, amount = 300_000, currency = "ALL", date, rate = 0.0105 });

        manual.StatusCode.ShouldBe(HttpStatusCode.Created);
        var manualBody = await manual.Content.ReadFromJsonAsync<JsonElement>(Ct);
        manualBody.GetProperty("rate").GetDouble().ShouldBe(0.0105);
        manualBody.GetProperty("rateManual").GetBoolean().ShouldBeTrue();
        manualBody.GetProperty("groupAmount").GetInt64().ShouldBe(3_150);
        Shares(manualBody).Sum(share => share.GroupAmount).ShouldBe(3_150);
    }

    [Fact]
    public async Task Should_Return503_When_ExpenseRateIsStale()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(
            isolated,
            (Currency.ALL, IsoDay(Today.AddDays(-40)), 0.0440m),
            (Currency.EUR, IsoDay(Today), 4.30m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(isolated, await UserIdAsync(client));

        using var stale = await PostAsync(
            client,
            group.Id,
            new { payerId = members[1].Id, amount = 300_000, currency = "ALL", date = IsoDay(Today) });

        stale.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await stale.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString().ShouldBe("rates.unavailable");
        (await StoredRowsAsync(isolated, group.Id)).ShouldBe((0, 0));

        using var manual = await PostAsync(
            client,
            group.Id,
            new { payerId = members[1].Id, amount = 300_000, currency = "ALL", date = IsoDay(Today), rate = 0.0105 });

        manual.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await manual.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("groupAmount").GetInt64().ShouldBe(3_150);
        (await StoredRowsAsync(isolated, group.Id)).ShouldBe((1, 1));
    }

    [Theory]
    [InlineData(1L, "JPY", 0.001, "validation.positiveAmount")]
    [InlineData(100_000_000_000L, "PLN", 1_000_000_000.0, "validation.invalidValue")]
    public async Task Should_RefuseExpense_When_ConvertedAmountIsOutOfRange(long amount, string currency, double rate, string code)
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(client));

        using var response = await PostAsync(
            client,
            group.Id,
            new { payerId = members[0].Id, amount, currency, date = IsoDay(Today), rate });

        await ShouldHaveErrorAsync(response, "rate", code);
        (await StoredRowsAsync(factory, group.Id)).ShouldBe((0, 0));
    }

    [Fact]
    public async Task Should_SplitExactly()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(client));
        var (olya, max) = (members[1].Id, members[2].Id);

        using var response = await PostAsync(
            client,
            group.Id,
            new
            {
                payerId = olya,
                amount = 30_000,
                currency = "PLN",
                date = IsoDay(Today),
                rate = 0.25,
                split = new
                {
                    mode = "exact",
                    shares = new[] { new { memberId = olya, amount = 10_000 }, new { memberId = max, amount = 20_000 } }
                }
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("rate").GetDouble().ShouldBe(0.25);
        body.GetProperty("rateManual").GetBoolean().ShouldBeTrue();
        body.GetProperty("groupAmount").GetInt64().ShouldBe(7_500);
        body.GetProperty("split").GetString().ShouldBe("exact");
        Shares(body).ShouldBe([(olya, 10_000L, 2_500L), (max, 20_000L, 5_000L)]);
    }

    [Fact]
    public async Task Should_SplitBetweenSome()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(client));
        var (anna, max) = (members[0].Id, members[2].Id);

        using var response = await PostAsync(
            client,
            group.Id,
            new
            {
                payerId = max,
                amount = 4_000,
                currency = "EUR",
                date = IsoDay(Today),
                split = new { mode = "equal", memberIds = new[] { max, anna } }
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("split").GetString().ShouldBe("equal");
        Shares(body).ShouldBe([(anna, 2_000L, 2_000L), (max, 2_000L, 2_000L)]);
    }

    [Fact]
    public async Task Should_DropMembersWithoutAMinorUnit_When_EqualSplitIsSmallerThanTheMemberCount()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(client), "Trip", Currency.JPY);
        var (anna, olya, max) = (members[0].Id, members[1].Id, members[2].Id);

        using var response = await PostAsync(
            client,
            group.Id,
            new { payerId = olya, amount = 2, currency = "JPY", date = IsoDay(Today) });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("split").GetString().ShouldBe("equal");
        body.GetProperty("groupAmount").GetInt64().ShouldBe(2);
        Shares(body).ShouldBe([(anna, 1L, 1L), (olya, 1L, 1L)]);
        (await StoredRowsAsync(factory, group.Id)).ShouldBe((1, 1));

        var balances = await GetAsync(client, $"{GroupsPath}/{group.Id}/balances");

        balances.GetProperty("total").GetInt64().ShouldBe(2);
        balances.GetProperty("members").EnumerateArray()
            .Select(member => (
                member.GetProperty("memberId").GetGuid(),
                member.GetProperty("paid").GetInt64(),
                member.GetProperty("share").GetInt64(),
                member.GetProperty("balance").GetInt64()))
            .ShouldBe([(anna, 0L, 1L, -1L), (olya, 2L, 1L, 1L), (max, 0L, 0L, 0L)]);
        balances.GetProperty("transfers").EnumerateArray()
            .Select(transfer => (
                transfer.GetProperty("fromMemberId").GetGuid(),
                transfer.GetProperty("toMemberId").GetGuid(),
                transfer.GetProperty("amount").GetInt64()))
            .ShouldBe([(anna, olya, 1L)]);
    }

    [Fact]
    public async Task Should_RefuseUnknownReferences()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var (group, members) = await TestData.AddGroupAsync(factory, userId, "Trip");
        var (other, otherMembers) = await TestData.AddGroupAsync(factory, userId, "Other");
        var foreignCategory = await AddCategoryAsync(client, other.Id, "Ferry");

        using var response = await PostAsync(
            client,
            group.Id,
            new
            {
                payerId = otherMembers[0].Id,
                amount = 1_000,
                currency = "EUR",
                date = IsoDay(Today),
                categoryId = foreignCategory,
                split = new { mode = "equal", memberIds = new[] { members[0].Id, otherMembers[1].Id } }
            });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        errors.EnumerateObject().Select(property => property.Name).ShouldBe(["payerId", "categoryId", "split"], ignoreOrder: true);
        Codes(errors, "payerId").ShouldBe(["validation.unknownMember"]);
        Codes(errors, "categoryId").ShouldBe(["validation.unknownCategory"]);
        Codes(errors, "split").ShouldBe(["validation.unknownMember"]);
        (await StoredRowsAsync(factory, group.Id)).ShouldBe((0, 0));
        (await StoredRowsAsync(factory, other.Id)).ShouldBe((0, 0));
    }

    [Fact]
    public async Task Should_RefuseExpense_When_GroupHasNoMembers()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(client), "Trip", Currency.EUR, "Anna");
        using var removed = await SendAsync(client, HttpMethod.Delete, $"{GroupsPath}/{group.Id}/members/{members[0].Id}");
        removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var response = await PostAsync(
            client,
            group.Id,
            new { payerId = members[0].Id, amount = 1_000, currency = "EUR", date = IsoDay(Today) });

        await ShouldHaveErrorAsync(response, "payerId", "validation.unknownMember");
        (await StoredRowsAsync(factory, group.Id)).ShouldBe((0, 0));
    }

    [Fact]
    public async Task Should_KeepTagsDescriptionAndCategory()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(client));
        var ferry = await AddCategoryAsync(client, group.Id, "Ferry");
        string[] tags = [" Beach ", "beach", "food"];

        using var own = await PostAsync(
            client,
            group.Id,
            new
            {
                payerId = members[0].Id,
                amount = 1_000,
                currency = "EUR",
                date = IsoDay(Today),
                categoryId = ferry,
                tags,
                description = "  Dinner "
            });
        using var standard = await PostAsync(
            client,
            group.Id,
            new { payerId = members[0].Id, amount = 1_000, currency = "EUR", date = IsoDay(Today), categoryId = "cafes" });

        own.StatusCode.ShouldBe(HttpStatusCode.Created);
        var ownBody = await own.Content.ReadFromJsonAsync<JsonElement>(Ct);
        ownBody.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()).ShouldBe(["Beach", "food"]);
        ownBody.GetProperty("description").GetString().ShouldBe("Dinner");
        ownBody.GetProperty("categoryId").GetString().ShouldBe(ferry);
        standard.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await standard.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("categoryId").GetString().ShouldBe("cafes");
    }

    [Fact]
    public async Task Should_ListExpenses_NewestFirst_AndDeletedSeparately()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(client));
        var oldest = await TestData.AddGroupExpenseAsync(factory, group, members[0], 1_000, members, IsoDay(Today.AddDays(-3)));
        var newest = await TestData.AddGroupExpenseAsync(factory, group, members[1], 3_000, members, IsoDay(Today.AddDays(-1)));
        var middle = await TestData.AddGroupExpenseAsync(factory, group, members[2], 2_000, members, IsoDay(Today.AddDays(-2)));
        var deleted = await TestData.AddGroupExpenseAsync(factory, group, members[0], 4_000, members, IsoDay(Today), deleted: true);

        var live = await GetAsync(client,$"{GroupsPath}/{group.Id}/expenses");

        live.EnumerateObject().Select(property => property.Name).ShouldBe(["items", "page", "pageSize", "total"], ignoreOrder: true);
        live.GetProperty("page").GetInt32().ShouldBe(1);
        live.GetProperty("pageSize").GetInt32().ShouldBe(100);
        live.GetProperty("total").GetInt32().ShouldBe(3);
        var items = live.GetProperty("items").EnumerateArray().ToList();
        items.Select(item => item.GetProperty("id").GetGuid()).ShouldBe([newest.Id, middle.Id, oldest.Id]);
        items.ShouldAllBe(item => !item.GetProperty("deleted").GetBoolean());
        items.ShouldAllBe(item => item.EnumerateObject().Count() == ExpenseProperties.Length);
        items[0].GetProperty("createdBy").GetProperty("kind").GetString().ShouldBe("owner");
        items[0].GetProperty("updatedBy").ValueKind.ShouldBe(JsonValueKind.Null);
        Shares(items[0]).Select(share => share.MemberId).ShouldBe(members.Select(member => member.Id));

        var removed = await GetAsync(client,$"{GroupsPath}/{group.Id}/expenses?deleted=true");

        removed.GetProperty("total").GetInt32().ShouldBe(1);
        var removedItems = removed.GetProperty("items").EnumerateArray().ToList();
        removedItems.Select(item => item.GetProperty("id").GetGuid()).ShouldBe([deleted.Id]);
        removedItems[0].GetProperty("deleted").GetBoolean().ShouldBeTrue();
        removedItems[0].GetProperty("updatedBy").GetProperty("kind").GetString().ShouldBe("owner");
        removedItems[0].GetProperty("updatedAt").ValueKind.ShouldBe(JsonValueKind.String);

        var explicitLive = await GetAsync(client,$"{GroupsPath}/{group.Id}/expenses?deleted=false");

        explicitLive.GetProperty("total").GetInt32().ShouldBe(3);

        using var invalid = await client.Http.GetAsync($"{GroupsPath}/{group.Id}/expenses?deleted=yes", Ct);

        await ShouldHaveErrorAsync(invalid, "deleted", "validation.invalidValue");

        var paged = await GetAsync(client,$"{GroupsPath}/{group.Id}/expenses?pageSize=2");

        paged.GetProperty("total").GetInt32().ShouldBe(3);
        paged.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())
            .ShouldBe([newest.Id, middle.Id]);
    }

    [Fact]
    public async Task Should_Return404_When_GroupUnknownOrForeign()
    {
        using var owner = new AuthClient(factory);
        using var intruder = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        await intruder.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(owner));
        await TestData.AddGroupExpenseAsync(factory, group, members[0], 1_000, members, IsoDay(Today));
        var body = new { payerId = members[0].Id, amount = 1_000, currency = "EUR", date = IsoDay(Today) };
        var unknown = Guid.NewGuid();

        using var foreignPost = await PostAsync(intruder, group.Id, body);
        using var foreignList = await intruder.Http.GetAsync($"{GroupsPath}/{group.Id}/expenses", Ct);
        using var unknownPost = await PostAsync(owner, unknown, body);
        using var unknownList = await owner.Http.GetAsync($"{GroupsPath}/{unknown}/expenses", Ct);
        using var notAGuid = await owner.Http.GetAsync($"{GroupsPath}/not-a-guid/expenses", Ct);

        foreach (var response in new[] { foreignPost, foreignList, unknownPost, unknownList, notAGuid })
        {
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        using var invalid = await PostAsync(owner, unknown, new { amount = 1_000, currency = "EUR", date = IsoDay(Today) });

        await ShouldHaveErrorAsync(invalid, "payerId", "validation.required");
        (await StoredRowsAsync(factory, group.Id)).ShouldBe((1, 1));
    }

    [Fact]
    public async Task Should_NotTouchPersonalData()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(factory, userId, openingBalance: 50_000);
        var (group, members) = await TestData.AddGroupAsync(factory, userId, "Trip", Currency.PLN);

        using var first = await PostAsync(
            client,
            group.Id,
            new { payerId = members[0].Id, amount = 12_000, currency = "PLN", date = IsoDay(Today), categoryId = "groceries" });
        using var second = await PostAsync(
            client,
            group.Id,
            new { payerId = members[1].Id, amount = 5_000, currency = "EUR", date = IsoDay(Today), rate = 4.3 });

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.Created);

        var overview = await GetAsync(client, "/v1/overview");

        overview.GetProperty("income").GetProperty("amount").GetInt64().ShouldBe(0);
        overview.GetProperty("expense").GetProperty("amount").GetInt64().ShouldBe(0);
        overview.GetProperty("transactionCount").GetInt32().ShouldBe(0);
        overview.GetProperty("hasTransactions").GetBoolean().ShouldBeFalse();

        var accounts = await GetAsync(client,"/v1/accounts");

        accounts.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == account.Id)
            .GetProperty("balance").GetInt64().ShouldBe(50_000);

        var report = await GetAsync(client, "/v1/reports");

        report.GetProperty("totals").GetProperty("income").GetInt64().ShouldBe(0);
        report.GetProperty("totals").GetProperty("expense").GetInt64().ShouldBe(0);
    }

    [Fact]
    public async Task Should_MarkMembersInUse_And_LockCurrency()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(client));

        using var response = await PostAsync(
            client,
            group.Id,
            new
            {
                payerId = members[0].Id,
                amount = 1_000,
                currency = "EUR",
                date = IsoDay(Today),
                split = new { mode = "equal", memberIds = new[] { members[1].Id } }
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var read = await GetAsync(client, $"{GroupsPath}/{group.Id}");
        read.GetProperty("members").EnumerateArray()
            .Select(member => (member.GetProperty("id").GetGuid(), member.GetProperty("inUse").GetBoolean()))
            .ShouldBe([(members[0].Id, true), (members[1].Id, true), (members[2].Id, false)]);
        read.GetProperty("currencyLocked").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Should_KeepReferencesConsistent_When_MemberRemovedConcurrently()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);

        for (var round = 0; round < 10; round++)
        {
            var (group, members) = await TestData.AddGroupAsync(factory, userId, $"Round {round}");
            var payer = members[2];
            var create = PostAsync(
                client,
                group.Id,
                new
                {
                    payerId = payer.Id,
                    amount = 900,
                    currency = "EUR",
                    date = IsoDay(Today),
                    split = new { mode = "equal", memberIds = new[] { members[0].Id } }
                });
            var remove = SendAsync(client, HttpMethod.Delete, $"{GroupsPath}/{group.Id}/members/{payer.Id}");
            await Task.WhenAll(create, remove);
            using var created = await create;
            using var removed = await remove;

            if (created.StatusCode == HttpStatusCode.Created)
            {
                await ShouldHaveErrorAsync(removed, "memberId", "validation.memberInUse");
                (await StoredRowsAsync(factory, group.Id)).ShouldBe((1, 1));
            }
            else
            {
                removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
                await ShouldHaveErrorAsync(created, "payerId", "validation.unknownMember");
                (await StoredRowsAsync(factory, group.Id)).ShouldBe((0, 0));
            }
        }
    }

    private static (Guid MemberId, long Amount, long GroupAmount)[] Shares(JsonElement expense) =>
    [
        .. expense.GetProperty("shares").EnumerateArray().Select(share => (
            share.GetProperty("memberId").GetGuid(),
            share.GetProperty("amount").GetInt64(),
            share.GetProperty("groupAmount").GetInt64()))
    ];

    private static string[] Codes(JsonElement errors, string field) =>
        [.. errors.GetProperty(field).EnumerateArray().Select(error => error.GetString()!)];

    private static string Canonical(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(
            ',',
            element.EnumerateObject()
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .Select(property => $"{property.Name}:{Canonical(property.Value)}")) + "}",
        JsonValueKind.Array => "[" + string.Join(',', element.EnumerateArray().Select(Canonical)) + "]",
        _ => element.GetRawText()
    };

    private static async Task ShouldHaveRecordedCreationAsync(
        WebApplicationFactory<Program> app,
        Guid ownerId,
        JsonElement expense)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var expenseId = expense.GetProperty("id").GetGuid();
        var events = await db.GroupEntryEvents.AsNoTracking()
            .Where(entryEvent => entryEvent.EntryId == expenseId)
            .ToListAsync(Ct);

        var created = events.ShouldHaveSingleItem();
        created.Kind.ShouldBe(GroupEntryKind.Expense);
        created.Action.ShouldBe(GroupEntryAction.Created);
        created.ActorUserId.ShouldBe(ownerId);
        created.ActorMemberId.ShouldBeNull();
        using var state = JsonDocument.Parse(created.State);
        state.RootElement.EnumerateObject().Select(property => property.Name).ShouldBe(StateProperties, ignoreOrder: true);
        foreach (var name in StateProperties)
        {
            Canonical(state.RootElement.GetProperty(name)).ShouldBe(Canonical(expense.GetProperty(name)), name);
        }
    }

    private static async Task<(int Expenses, int Events)> StoredRowsAsync(WebApplicationFactory<Program> app, Guid groupId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (
            await db.GroupExpenses.CountAsync(expense => expense.GroupId == groupId, Ct),
            await db.GroupEntryEvents.CountAsync(entryEvent => entryEvent.GroupId == groupId, Ct));
    }

    private static async Task<string> AddCategoryAsync(AuthClient client, Guid groupId, string name)
    {
        using var response = await SendAsync(client, HttpMethod.Post, $"{GroupsPath}/{groupId}/categories", new { name });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetString()!;
    }

    private static string IsoDay(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static Task<HttpResponseMessage> PostAsync(AuthClient client, Guid groupId, object body) =>
        SendAsync(client, HttpMethod.Post, $"{GroupsPath}/{groupId}/expenses", body);

    private static Task<HttpResponseMessage> SendAsync(AuthClient client, HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.Http.SendAsync(client.WithXsrf(request), Ct);
    }

    private static async Task<JsonElement> GetAsync(AuthClient client, string path)
    {
        using var response = await client.Http.GetAsync(path, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task ShouldHaveErrorAsync(HttpResponseMessage response, string field, string code)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        errors.EnumerateObject().Select(property => property.Name).ShouldBe([field]);
        Codes(errors, field).ShouldBe([code]);
    }

    private static async Task<Guid> UserIdAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync("/v1/me", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }
}
