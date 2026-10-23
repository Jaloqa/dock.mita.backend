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

public sealed class GroupRepaymentsTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string GroupsPath = "/v1/groups";

    private static readonly string[] RepaymentProperties =
    [
        "id", "fromMemberId", "toMemberId", "amount", "currency", "date", "rate", "rateManual", "groupAmount", "note",
        "deleted", "createdBy", "createdAt", "updatedBy", "updatedAt"
    ];

    private static readonly string[] StateProperties =
    [
        "fromMemberId", "toMemberId", "amount", "currency", "date", "rate", "rateManual", "groupAmount", "note"
    ];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Should_RecordRepayment()
    {
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var (group, members) = await TestData.AddGroupAsync(factory, userId);
        var (anna, olya, max) = (members[0], members[1], members[2]);

        using var response = await PostAsync(
            client,
            group.Id,
            new { fromMemberId = max.Id, toMemberId = anna.Id, amount = 2_000, currency = "EUR", date = IsoDay(Today) });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldBeNull();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.EnumerateObject().Select(property => property.Name).ShouldBe(RepaymentProperties, ignoreOrder: true);
        body.GetProperty("fromMemberId").GetGuid().ShouldBe(max.Id);
        body.GetProperty("toMemberId").GetGuid().ShouldBe(anna.Id);
        body.GetProperty("amount").GetInt64().ShouldBe(2_000);
        body.GetProperty("currency").GetString().ShouldBe("EUR");
        body.GetProperty("date").GetString().ShouldBe(IsoDay(Today));
        body.GetProperty("rate").GetDouble().ShouldBe(1);
        body.GetProperty("rateManual").GetBoolean().ShouldBeFalse();
        body.GetProperty("groupAmount").GetInt64().ShouldBe(2_000);
        body.GetProperty("note").ValueKind.ShouldBe(JsonValueKind.Null);
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

        var read = await GetAsync(client, $"{GroupsPath}/{group.Id}");
        read.GetProperty("members").EnumerateArray()
            .Select(member => (member.GetProperty("id").GetGuid(), member.GetProperty("inUse").GetBoolean()))
            .ShouldBe([(anna.Id, true), (olya.Id, false), (max.Id, true)]);
        read.GetProperty("currencyLocked").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Should_ConvertRepayment_InAnyCurrency()
    {
        await using var isolated = factory.WithIsolatedData();
        var date = IsoDay(Today.AddDays(-3));
        await TestData.AddRatesAsync(isolated, (Currency.EUR, date, 4.30m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(isolated, await UserIdAsync(client));
        var (from, to) = (members[2].Id, members[0].Id);

        using var stored = await PostAsync(
            client,
            group.Id,
            new { fromMemberId = from, toMemberId = to, amount = 10_000, currency = "PLN", date });

        stored.StatusCode.ShouldBe(HttpStatusCode.Created);
        var storedBody = await stored.Content.ReadFromJsonAsync<JsonElement>(Ct);
        storedBody.GetProperty("currency").GetString().ShouldBe("PLN");
        storedBody.GetProperty("rate").GetDouble().ShouldBe(0.232558);
        storedBody.GetProperty("rateManual").GetBoolean().ShouldBeFalse();
        storedBody.GetProperty("groupAmount").GetInt64().ShouldBe(2_326);
        await ShouldHaveRecordedCreationAsync(isolated, await UserIdAsync(client), storedBody);

        using var manual = await PostAsync(
            client,
            group.Id,
            new { fromMemberId = from, toMemberId = to, amount = 10_000, currency = "PLN", date, rate = 0.25 });

        manual.StatusCode.ShouldBe(HttpStatusCode.Created);
        var manualBody = await manual.Content.ReadFromJsonAsync<JsonElement>(Ct);
        manualBody.GetProperty("rate").GetDouble().ShouldBe(0.25);
        manualBody.GetProperty("rateManual").GetBoolean().ShouldBeTrue();
        manualBody.GetProperty("groupAmount").GetInt64().ShouldBe(2_500);

        using var sameCurrency = await PostAsync(
            client,
            group.Id,
            new { fromMemberId = from, toMemberId = to, amount = 5_000, currency = "EUR", date, rate = 9 });

        sameCurrency.StatusCode.ShouldBe(HttpStatusCode.Created);
        var sameBody = await sameCurrency.Content.ReadFromJsonAsync<JsonElement>(Ct);
        sameBody.GetProperty("rate").GetDouble().ShouldBe(1);
        sameBody.GetProperty("rateManual").GetBoolean().ShouldBeFalse();
        sameBody.GetProperty("groupAmount").GetInt64().ShouldBe(5_000);

        await using var stale = factory.WithIsolatedData();
        await TestData.AddRatesAsync(stale, (Currency.EUR, IsoDay(Today.AddDays(-40)), 4.30m));
        using var staleClient = new AuthClient(stale);
        await staleClient.RegisterNewAsync(Ct);
        var (staleGroup, staleMembers) = await TestData.AddGroupAsync(stale, await UserIdAsync(staleClient));

        using var unavailable = await PostAsync(
            staleClient,
            staleGroup.Id,
            new
            {
                fromMemberId = staleMembers[2].Id,
                toMemberId = staleMembers[0].Id,
                amount = 10_000,
                currency = "PLN",
                date = IsoDay(Today)
            });

        unavailable.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await unavailable.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString()
            .ShouldBe("rates.unavailable");
        (await StoredRowsAsync(stale, staleGroup.Id)).ShouldBe((0, 0));
    }

    [Theory]
    [InlineData(1L, "JPY", 0.001, "validation.positiveAmount")]
    [InlineData(100_000_000_000L, "PLN", 1_000_000_000.0, "validation.invalidValue")]
    public async Task Should_RefuseRepayment_When_ConvertedAmountIsOutOfRange(long amount, string currency, double rate, string code)
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(client));

        using var response = await PostAsync(
            client,
            group.Id,
            new { fromMemberId = members[2].Id, toMemberId = members[0].Id, amount, currency, date = IsoDay(Today), rate });

        await ShouldHaveErrorsAsync(response, ("rate", code));
        (await StoredRowsAsync(factory, group.Id)).ShouldBe((0, 0));
    }

    [Fact]
    public async Task Should_RepriceOnEdit_UnlessRateWasManual()
    {
        await using var isolated = factory.WithIsolatedData();
        var (older, newer) = (IsoDay(Today.AddDays(-10)), IsoDay(Today.AddDays(-3)));
        await TestData.AddRatesAsync(isolated, (Currency.EUR, older, 4.00m), (Currency.EUR, newer, 5.00m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(isolated, await UserIdAsync(client));
        var (from, to) = (members[1].Id, members[0].Id);
        var created = await CreateAsync(
            client,
            group.Id,
            new { fromMemberId = from, toMemberId = to, amount = 10_000, currency = "PLN", date = older });
        created.GetProperty("rate").GetDouble().ShouldBe(0.25);
        created.GetProperty("groupAmount").GetInt64().ShouldBe(2_500);
        var path = RepaymentPath(group.Id, created.GetProperty("id").GetGuid());

        using var repriced = await SendAsync(
            client,
            HttpMethod.Put,
            path,
            new { fromMemberId = from, toMemberId = to, amount = 10_000, currency = "PLN", date = newer });

        repriced.StatusCode.ShouldBe(HttpStatusCode.OK);
        var repricedBody = await repriced.Content.ReadFromJsonAsync<JsonElement>(Ct);
        repricedBody.GetProperty("rate").GetDouble().ShouldBe(0.2);
        repricedBody.GetProperty("groupAmount").GetInt64().ShouldBe(2_000);
        repricedBody.GetProperty("rateManual").GetBoolean().ShouldBeFalse();

        using var manual = await SendAsync(
            client,
            HttpMethod.Put,
            path,
            new { fromMemberId = from, toMemberId = to, amount = 10_000, currency = "PLN", date = newer, rate = 0.3 });

        manual.StatusCode.ShouldBe(HttpStatusCode.OK);
        var manualBody = await manual.Content.ReadFromJsonAsync<JsonElement>(Ct);
        manualBody.GetProperty("rate").GetDouble().ShouldBe(0.3);
        manualBody.GetProperty("groupAmount").GetInt64().ShouldBe(3_000);
        manualBody.GetProperty("rateManual").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Should_RefuseMembers()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var (group, members) = await TestData.AddGroupAsync(factory, userId, "Trip");
        var (other, otherMembers) = await TestData.AddGroupAsync(factory, userId, "Other");

        using var same = await PostAsync(
            client,
            group.Id,
            new { fromMemberId = members[1].Id, toMemberId = members[1].Id, amount = 1_000, currency = "EUR", date = IsoDay(Today) });

        await ShouldHaveErrorsAsync(same, ("toMemberId", "validation.sameMember"));

        using var foreign = await PostAsync(
            client,
            group.Id,
            new { fromMemberId = otherMembers[0].Id, toMemberId = otherMembers[1].Id, amount = 1_000, currency = "EUR", date = IsoDay(Today) });

        await ShouldHaveErrorsAsync(foreign, ("fromMemberId", "validation.unknownMember"), ("toMemberId", "validation.unknownMember"));

        using var half = await PostAsync(
            client,
            group.Id,
            new { fromMemberId = members[0].Id, toMemberId = otherMembers[1].Id, amount = 1_000, currency = "EUR", date = IsoDay(Today) });

        await ShouldHaveErrorsAsync(half, ("toMemberId", "validation.unknownMember"));
        (await StoredRowsAsync(factory, group.Id)).ShouldBe((0, 0));
        (await StoredRowsAsync(factory, other.Id)).ShouldBe((0, 0));
    }

    [Fact]
    public async Task Should_EditDeleteRestoreRepayment_WithHistory()
    {
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(client));
        var (anna, olya, max) = (members[0].Id, members[1].Id, members[2].Id);
        var created = await CreateAsync(
            client,
            group.Id,
            new { fromMemberId = max, toMemberId = anna, amount = 2_000, currency = "EUR", date = IsoDay(Today) });
        var repaymentId = created.GetProperty("id").GetGuid();
        var path = RepaymentPath(group.Id, repaymentId);

        using var updated = await SendAsync(
            client,
            HttpMethod.Put,
            path,
            new { fromMemberId = olya, toMemberId = anna, amount = 2_500, currency = "EUR", date = IsoDay(Today), note = " cash " });

        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updatedBody = await updated.Content.ReadFromJsonAsync<JsonElement>(Ct);
        updatedBody.GetProperty("id").GetGuid().ShouldBe(repaymentId);
        updatedBody.GetProperty("fromMemberId").GetGuid().ShouldBe(olya);
        updatedBody.GetProperty("amount").GetInt64().ShouldBe(2_500);
        updatedBody.GetProperty("groupAmount").GetInt64().ShouldBe(2_500);
        updatedBody.GetProperty("note").GetString().ShouldBe("cash");
        Canonical(updatedBody.GetProperty("createdBy")).ShouldBe(Canonical(created.GetProperty("createdBy")));
        updatedBody.GetProperty("createdAt").GetString().ShouldBe(created.GetProperty("createdAt").GetString());
        var updatedBy = updatedBody.GetProperty("updatedBy");
        updatedBy.GetProperty("kind").GetString().ShouldBe("owner");
        updatedBy.GetProperty("memberId").ValueKind.ShouldBe(JsonValueKind.Null);
        updatedBy.GetProperty("name").GetString().ShouldBe(credentials.Name);
        updatedBody.GetProperty("updatedAt").GetString()!.ShouldMatch(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$");

        using var deleted = await SendAsync(client, HttpMethod.Delete, path);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetAsync(client, $"{GroupsPath}/{group.Id}/repayments")).GetProperty("total").GetInt32().ShouldBe(0);
        var removed = await GetAsync(client, $"{GroupsPath}/{group.Id}/repayments?deleted=true");
        removed.GetProperty("total").GetInt32().ShouldBe(1);
        var removedItem = removed.GetProperty("items")[0];
        removedItem.GetProperty("id").GetGuid().ShouldBe(repaymentId);
        removedItem.GetProperty("deleted").GetBoolean().ShouldBeTrue();
        removedItem.GetProperty("updatedBy").GetProperty("kind").GetString().ShouldBe("owner");

        using var editDeleted = await SendAsync(
            client,
            HttpMethod.Put,
            path,
            new { fromMemberId = max, toMemberId = anna, amount = 1_000, currency = "EUR", date = IsoDay(Today) });
        using var deleteAgain = await SendAsync(client, HttpMethod.Delete, path);

        editDeleted.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        deleteAgain.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var restored = await SendAsync(client, HttpMethod.Post, $"{path}/restore");

        restored.StatusCode.ShouldBe(HttpStatusCode.OK);
        var restoredBody = await restored.Content.ReadFromJsonAsync<JsonElement>(Ct);
        restoredBody.GetProperty("id").GetGuid().ShouldBe(repaymentId);
        restoredBody.GetProperty("deleted").GetBoolean().ShouldBeFalse();
        restoredBody.GetProperty("amount").GetInt64().ShouldBe(2_500);
        restoredBody.GetProperty("note").GetString().ShouldBe("cash");
        (await GetAsync(client, $"{GroupsPath}/{group.Id}/repayments")).GetProperty("total").GetInt32().ShouldBe(1);

        using var restoreAgain = await SendAsync(client, HttpMethod.Post, $"{path}/restore");

        restoreAgain.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var history = await GetAsync(client, $"{path}/history");

        history.EnumerateObject().Select(property => property.Name).ShouldBe(["items", "page", "pageSize", "total"], ignoreOrder: true);
        history.GetProperty("page").GetInt32().ShouldBe(1);
        history.GetProperty("pageSize").GetInt32().ShouldBe(100);
        history.GetProperty("total").GetInt32().ShouldBe(4);
        var events = history.GetProperty("items").EnumerateArray().ToList();
        events.Select(entry => entry.GetProperty("action").GetString()).ShouldBe(["restored", "deleted", "updated", "created"]);
        foreach (var entry in events)
        {
            entry.EnumerateObject().Select(property => property.Name).ShouldBe(["id", "action", "at", "actor", "state"], ignoreOrder: true);
            entry.GetProperty("at").GetString()!.ShouldMatch(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$");
            var actor = entry.GetProperty("actor");
            actor.GetProperty("kind").GetString().ShouldBe("owner");
            actor.GetProperty("name").GetString().ShouldBe(credentials.Name);
            entry.GetProperty("state").EnumerateObject().Select(property => property.Name).ShouldBe(StateProperties, ignoreOrder: true);
        }

        events.Take(3).ShouldAllBe(entry => entry.GetProperty("state").GetProperty("amount").GetInt64() == 2_500);
        events.Take(3).ShouldAllBe(entry => entry.GetProperty("state").GetProperty("note").GetString() == "cash");
        events[3].GetProperty("state").GetProperty("amount").GetInt64().ShouldBe(2_000);
        events[3].GetProperty("state").GetProperty("fromMemberId").GetGuid().ShouldBe(max);
        events[3].GetProperty("state").GetProperty("note").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Should_ListRepayments_NewestFirst()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(client));
        var (anna, olya, max) = (members[0], members[1], members[2]);
        var oldest = await TestData.AddGroupRepaymentAsync(factory, group, max, anna, 1_000, IsoDay(Today.AddDays(-3)));
        var newest = await TestData.AddGroupRepaymentAsync(factory, group, olya, anna, 3_000, IsoDay(Today.AddDays(-1)));
        var middle = await TestData.AddGroupRepaymentAsync(factory, group, max, olya, 2_000, IsoDay(Today.AddDays(-2)));
        var deleted = await TestData.AddGroupRepaymentAsync(factory, group, anna, max, 4_000, IsoDay(Today), deleted: true);

        var live = await GetAsync(client, $"{GroupsPath}/{group.Id}/repayments");

        live.EnumerateObject().Select(property => property.Name).ShouldBe(["items", "page", "pageSize", "total"], ignoreOrder: true);
        live.GetProperty("page").GetInt32().ShouldBe(1);
        live.GetProperty("pageSize").GetInt32().ShouldBe(100);
        live.GetProperty("total").GetInt32().ShouldBe(3);
        var items = live.GetProperty("items").EnumerateArray().ToList();
        items.Select(item => item.GetProperty("id").GetGuid()).ShouldBe([newest.Id, middle.Id, oldest.Id]);
        items.ShouldAllBe(item => !item.GetProperty("deleted").GetBoolean());
        items.ShouldAllBe(item => item.EnumerateObject().Count() == RepaymentProperties.Length);
        items[0].GetProperty("fromMemberId").GetGuid().ShouldBe(olya.Id);
        items[0].GetProperty("toMemberId").GetGuid().ShouldBe(anna.Id);
        items[0].GetProperty("createdBy").GetProperty("kind").GetString().ShouldBe("owner");
        items[0].GetProperty("updatedBy").ValueKind.ShouldBe(JsonValueKind.Null);

        var removed = await GetAsync(client, $"{GroupsPath}/{group.Id}/repayments?deleted=true");

        removed.GetProperty("total").GetInt32().ShouldBe(1);
        var removedItems = removed.GetProperty("items").EnumerateArray().ToList();
        removedItems.Select(item => item.GetProperty("id").GetGuid()).ShouldBe([deleted.Id]);
        removedItems[0].GetProperty("deleted").GetBoolean().ShouldBeTrue();
        removedItems[0].GetProperty("updatedBy").GetProperty("kind").GetString().ShouldBe("owner");
        removedItems[0].GetProperty("updatedAt").ValueKind.ShouldBe(JsonValueKind.String);

        (await GetAsync(client, $"{GroupsPath}/{group.Id}/repayments?deleted=false")).GetProperty("total").GetInt32().ShouldBe(3);

        using var invalid = await client.Http.GetAsync($"{GroupsPath}/{group.Id}/repayments?deleted=yes", Ct);

        await ShouldHaveErrorsAsync(invalid, ("deleted", "validation.invalidValue"));

        var paged = await GetAsync(client, $"{GroupsPath}/{group.Id}/repayments?pageSize=2");

        paged.GetProperty("total").GetInt32().ShouldBe(3);
        paged.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())
            .ShouldBe([newest.Id, middle.Id]);
    }

    [Fact]
    public async Task Should_ListSameDayRepayments_NewestCreatedFirst()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(client));
        var day = IsoDay(Today.AddDays(-1));
        var first = await CreateAsync(
            client,
            group.Id,
            new { fromMemberId = members[2].Id, toMemberId = members[0].Id, amount = 1_000, currency = "EUR", date = day });
        var second = await CreateAsync(
            client,
            group.Id,
            new { fromMemberId = members[1].Id, toMemberId = members[0].Id, amount = 2_000, currency = "EUR", date = day });
        var earlierDay = await CreateAsync(
            client,
            group.Id,
            new
            {
                fromMemberId = members[2].Id,
                toMemberId = members[1].Id,
                amount = 3_000,
                currency = "EUR",
                date = IsoDay(Today.AddDays(-2))
            });

        var listed = await GetAsync(client, $"{GroupsPath}/{group.Id}/repayments");

        listed.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ShouldBe(
        [
            second.GetProperty("id").GetGuid(),
            first.GetProperty("id").GetGuid(),
            earlierDay.GetProperty("id").GetGuid()
        ]);
    }

    [Fact]
    public async Task Should_Return404_When_GroupUnknownOrForeign()
    {
        using var owner = new AuthClient(factory);
        using var intruder = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        await intruder.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(owner));
        await TestData.AddGroupRepaymentAsync(factory, group, members[2], members[0], 1_000, IsoDay(Today));
        var body = new { fromMemberId = members[2].Id, toMemberId = members[0].Id, amount = 1_000, currency = "EUR", date = IsoDay(Today) };
        var unknown = Guid.NewGuid();

        using var foreignPost = await PostAsync(intruder, group.Id, body);
        using var foreignList = await intruder.Http.GetAsync($"{GroupsPath}/{group.Id}/repayments", Ct);
        using var unknownPost = await PostAsync(owner, unknown, body);
        using var unknownList = await owner.Http.GetAsync($"{GroupsPath}/{unknown}/repayments", Ct);
        using var notAGuid = await owner.Http.GetAsync($"{GroupsPath}/not-a-guid/repayments", Ct);

        foreach (var response in new[] { foreignPost, foreignList, unknownPost, unknownList, notAGuid })
        {
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        using var invalid = await PostAsync(owner, unknown, new { amount = 1_000, currency = "EUR", date = IsoDay(Today) });

        await ShouldHaveErrorsAsync(
            invalid,
            ("fromMemberId", "validation.required"),
            ("toMemberId", "validation.required"));
        (await StoredRowsAsync(factory, group.Id)).ShouldBe((1, 1));
    }

    [Fact]
    public async Task Should_Return404_ForEntriesOfAnotherGroup()
    {
        using var owner = new AuthClient(factory);
        using var intruder = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        await intruder.RegisterNewAsync(Ct);
        var ownerId = await UserIdAsync(owner);
        var (groupX, membersX) = await TestData.AddGroupAsync(factory, ownerId, "X");
        var (groupY, membersY) = await TestData.AddGroupAsync(factory, ownerId, "Y");
        var live = await TestData.AddGroupRepaymentAsync(factory, groupX, membersX[2], membersX[0], 1_000, IsoDay(Today));
        var removed = await TestData.AddGroupRepaymentAsync(factory, groupX, membersX[2], membersX[0], 2_000, IsoDay(Today), deleted: true);
        var liveBody = new { fromMemberId = membersY[2].Id, toMemberId = membersY[0].Id, amount = 1_000, currency = "EUR", date = IsoDay(Today) };
        var foreignBody = new { fromMemberId = membersX[2].Id, toMemberId = membersX[0].Id, amount = 1_000, currency = "EUR", date = IsoDay(Today) };

        var underOtherGroup = new[]
        {
            await SendAsync(owner, HttpMethod.Put, RepaymentPath(groupY.Id, live.Id), liveBody),
            await SendAsync(owner, HttpMethod.Delete, RepaymentPath(groupY.Id, live.Id)),
            await SendAsync(owner, HttpMethod.Post, $"{RepaymentPath(groupY.Id, removed.Id)}/restore"),
            await SendAsync(owner, HttpMethod.Get, $"{RepaymentPath(groupY.Id, live.Id)}/history"),
            await SendAsync(owner, HttpMethod.Get, $"{RepaymentPath(groupY.Id, removed.Id)}/history")
        };
        var byIntruder = new[]
        {
            await SendAsync(intruder, HttpMethod.Put, RepaymentPath(groupX.Id, live.Id), foreignBody),
            await SendAsync(intruder, HttpMethod.Delete, RepaymentPath(groupX.Id, live.Id)),
            await SendAsync(intruder, HttpMethod.Post, $"{RepaymentPath(groupX.Id, removed.Id)}/restore"),
            await SendAsync(intruder, HttpMethod.Get, $"{RepaymentPath(groupX.Id, live.Id)}/history")
        };
        var unknown = new[]
        {
            await SendAsync(owner, HttpMethod.Put, RepaymentPath(groupX.Id, Guid.NewGuid()), foreignBody),
            await SendAsync(owner, HttpMethod.Delete, RepaymentPath(groupX.Id, Guid.NewGuid())),
            await SendAsync(owner, HttpMethod.Post, $"{RepaymentPath(groupX.Id, Guid.NewGuid())}/restore"),
            await SendAsync(owner, HttpMethod.Get, $"{RepaymentPath(groupX.Id, Guid.NewGuid())}/history"),
            await SendAsync(owner, HttpMethod.Delete, $"{GroupsPath}/{groupX.Id}/repayments/not-a-guid")
        };

        foreach (var response in underOtherGroup.Concat(byIntruder).Concat(unknown))
        {
            using (response)
            {
                response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            }
        }

        var untouched = await GetAsync(owner, $"{GroupsPath}/{groupX.Id}/repayments");
        untouched.GetProperty("total").GetInt32().ShouldBe(1);
        untouched.GetProperty("items")[0].GetProperty("updatedBy").ValueKind.ShouldBe(JsonValueKind.Null);
        (await GetAsync(owner, $"{GroupsPath}/{groupX.Id}/repayments?deleted=true")).GetProperty("total").GetInt32().ShouldBe(1);

        using var invalid = await SendAsync(
            owner,
            HttpMethod.Put,
            RepaymentPath(groupX.Id, Guid.NewGuid()),
            new { amount = 1_000, currency = "EUR", date = IsoDay(Today) });

        await ShouldHaveErrorsAsync(
            invalid,
            ("fromMemberId", "validation.required"),
            ("toMemberId", "validation.required"));
    }

    [Fact]
    public async Task Should_RefuseMembers_When_RepaymentIsEdited()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var (group, members) = await TestData.AddGroupAsync(factory, userId, "Trip");
        var (_, otherMembers) = await TestData.AddGroupAsync(factory, userId, "Other");
        var created = await CreateAsync(
            client,
            group.Id,
            new { fromMemberId = members[2].Id, toMemberId = members[0].Id, amount = 2_000, currency = "EUR", date = IsoDay(Today) });
        var path = RepaymentPath(group.Id, created.GetProperty("id").GetGuid());

        using var response = await SendAsync(
            client,
            HttpMethod.Put,
            path,
            new { fromMemberId = otherMembers[0].Id, toMemberId = members[0].Id, amount = 3_000, currency = "EUR", date = IsoDay(Today) });

        await ShouldHaveErrorsAsync(response, ("fromMemberId", "validation.unknownMember"));
        var unchanged = (await GetAsync(client, $"{GroupsPath}/{group.Id}/repayments")).GetProperty("items")[0];
        unchanged.GetProperty("amount").GetInt64().ShouldBe(2_000);
        unchanged.GetProperty("updatedBy").ValueKind.ShouldBe(JsonValueKind.Null);
        (await GetAsync(client, $"{path}/history")).GetProperty("total").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Should_KeepMemberInUse_When_RepaymentIsDeleted()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(client));
        var created = await CreateAsync(
            client,
            group.Id,
            new { fromMemberId = members[2].Id, toMemberId = members[0].Id, amount = 2_000, currency = "EUR", date = IsoDay(Today) });
        using var deleted = await SendAsync(client, HttpMethod.Delete, RepaymentPath(group.Id, created.GetProperty("id").GetGuid()));
        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var removal = await SendAsync(client, HttpMethod.Delete, $"{GroupsPath}/{group.Id}/members/{members[2].Id}");

        await ShouldHaveErrorsAsync(removal, ("memberId", "validation.memberInUse"));
        var read = await GetAsync(client, $"{GroupsPath}/{group.Id}");
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
                new { fromMemberId = payer.Id, toMemberId = members[0].Id, amount = 900, currency = "EUR", date = IsoDay(Today) });
            var remove = SendAsync(client, HttpMethod.Delete, $"{GroupsPath}/{group.Id}/members/{payer.Id}");
            await Task.WhenAll(create, remove);
            using var created = await create;
            using var removed = await remove;

            if (created.StatusCode == HttpStatusCode.Created)
            {
                await ShouldHaveErrorsAsync(removed, ("memberId", "validation.memberInUse"));
                (await StoredRowsAsync(factory, group.Id)).ShouldBe((1, 1));
            }
            else
            {
                removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
                await ShouldHaveErrorsAsync(created, ("fromMemberId", "validation.unknownMember"));
                (await StoredRowsAsync(factory, group.Id)).ShouldBe((0, 0));
            }
        }
    }

    [Fact]
    public async Task Should_NotEditDeletedRepayment_When_DeletedConcurrently()
    {
        await using var isolated = factory.WithIsolatedData();
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(isolated, await UserIdAsync(client));
        var created = await CreateAsync(client, group.Id, EurRepayment(members, 2_000));
        var repaymentId = created.GetProperty("id").GetGuid();
        var path = RepaymentPath(group.Id, repaymentId);

        var responses = await SendBehindRepaymentLockAsync(
            isolated,
            repaymentId,
            () => SendAsync(client, HttpMethod.Delete, path),
            () => SendAsync(client, HttpMethod.Put, path, EurRepayment(members, 3_000)));

        using var deleted = responses[0];
        using var edited = responses[1];
        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        edited.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var history = (await GetAsync(client, $"{path}/history")).GetProperty("items").EnumerateArray().ToList();
        history.Select(entry => entry.GetProperty("action").GetString()).ShouldBe(["deleted", "created"]);
        var stored = (await GetAsync(client, $"{GroupsPath}/{group.Id}/repayments?deleted=true")).GetProperty("items").EnumerateArray().Single();
        stored.GetProperty("amount").GetInt64().ShouldBe(2_000);
        history[0].GetProperty("state").GetProperty("amount").GetInt64().ShouldBe(2_000);
    }

    [Fact]
    public async Task Should_NotRestoreRepayment_When_RestoredConcurrently()
    {
        await using var isolated = factory.WithIsolatedData();
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var (group, members) = await TestData.AddGroupAsync(isolated, userId);
        var removed = await TestData.AddGroupRepaymentAsync(isolated, group, members[2], members[0], 2_000, IsoDay(Today), deleted: true);
        var path = RepaymentPath(group.Id, removed.Id);

        var responses = await SendBehindRepaymentLockAsync(
            isolated,
            removed.Id,
            () => SendAsync(client, HttpMethod.Post, $"{path}/restore"),
            () => SendAsync(client, HttpMethod.Post, $"{path}/restore"));

        using var first = responses[0];
        using var second = responses[1];
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var history = (await GetAsync(client, $"{path}/history")).GetProperty("items").EnumerateArray().ToList();
        history.Select(entry => entry.GetProperty("action").GetString()).ShouldBe(["restored", "deleted", "created"]);
        (await GetAsync(client, $"{GroupsPath}/{group.Id}/repayments")).GetProperty("total").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Should_ApplyBothEdits_When_EditWaitsForEdit()
    {
        await using var isolated = factory.WithIsolatedData();
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(isolated, await UserIdAsync(client));
        var created = await CreateAsync(client, group.Id, EurRepayment(members, 2_000));
        var repaymentId = created.GetProperty("id").GetGuid();
        var path = RepaymentPath(group.Id, repaymentId);

        var responses = await SendBehindRepaymentLockAsync(
            isolated,
            repaymentId,
            () => SendAsync(client, HttpMethod.Put, path, EurRepayment(members, 3_000)),
            () => SendAsync(client, HttpMethod.Put, path, EurRepayment(members, 4_000)));

        foreach (var response in responses)
        {
            using (response)
            {
                response.StatusCode.ShouldBe(HttpStatusCode.OK);
            }
        }

        var stored = (await GetAsync(client, $"{GroupsPath}/{group.Id}/repayments")).GetProperty("items").EnumerateArray().Single();
        stored.GetProperty("amount").GetInt64().ShouldBe(4_000);
        var history = (await GetAsync(client, $"{path}/history")).GetProperty("items").EnumerateArray().ToList();
        history.Select(entry => entry.GetProperty("action").GetString()).ShouldBe(["updated", "updated", "created"]);
        history.Select(entry => entry.GetProperty("state").GetProperty("amount").GetInt64()).ShouldBe([4_000L, 3_000L, 2_000L]);
    }

    private static object EurRepayment(GroupMember[] members, long amount) => new
    {
        fromMemberId = members[2].Id,
        toMemberId = members[0].Id,
        amount,
        currency = "EUR",
        date = IsoDay(Today)
    };

    private static async Task<HttpResponseMessage[]> SendBehindRepaymentLockAsync(
        WebApplicationFactory<Program> app,
        Guid repaymentId,
        params Func<Task<HttpResponseMessage>>[] requests)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var holder = await db.Database.BeginTransactionAsync(Ct);
        await db.Database.ExecuteSqlAsync($"""SELECT 1 FROM group_repayments WHERE "Id" = {repaymentId} FOR UPDATE""", Ct);

        var pending = new List<Task<HttpResponseMessage>>();
        foreach (var request in requests)
        {
            pending.Add(request());
            await WaitUntilBlockedAsync(app, pending);
        }

        await holder.CommitAsync(Ct);
        return await Task.WhenAll(pending);
    }

    private static async Task WaitUntilBlockedAsync(WebApplicationFactory<Program> app, List<Task<HttpResponseMessage>> pending)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            pending.ShouldAllBe(request => !request.IsCompleted, "A request finished while the repayment row was locked.");
            var blocked = await db.Database
                .SqlQuery<int>($"""SELECT count(*)::int AS "Value" FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock'""")
                .SingleAsync(Ct);
            if (blocked >= pending.Count)
            {
                return;
            }

            await Task.Delay(20, Ct);
        }

        throw new TimeoutException("The requests did not wait for the repayment row lock.");
    }

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
        JsonElement repayment)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var repaymentId = repayment.GetProperty("id").GetGuid();
        var events = await db.GroupEntryEvents.AsNoTracking()
            .Where(entryEvent => entryEvent.EntryId == repaymentId)
            .ToListAsync(Ct);

        var created = events.ShouldHaveSingleItem();
        created.Kind.ShouldBe(GroupEntryKind.Repayment);
        created.Action.ShouldBe(GroupEntryAction.Created);
        created.ActorUserId.ShouldBe(ownerId);
        created.ActorMemberId.ShouldBeNull();
        using var state = JsonDocument.Parse(created.State);
        state.RootElement.EnumerateObject().Select(property => property.Name).ShouldBe(StateProperties, ignoreOrder: true);
        foreach (var name in StateProperties)
        {
            Canonical(state.RootElement.GetProperty(name)).ShouldBe(Canonical(repayment.GetProperty(name)), name);
        }
    }

    private static async Task<(int Repayments, int Events)> StoredRowsAsync(WebApplicationFactory<Program> app, Guid groupId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (
            await db.GroupRepayments.CountAsync(repayment => repayment.GroupId == groupId, Ct),
            await db.GroupEntryEvents.CountAsync(entryEvent => entryEvent.GroupId == groupId, Ct));
    }

    private static string RepaymentPath(Guid groupId, Guid repaymentId) => $"{GroupsPath}/{groupId}/repayments/{repaymentId}";

    private static string IsoDay(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static async Task<JsonElement> CreateAsync(AuthClient client, Guid groupId, object body)
    {
        using var response = await PostAsync(client, groupId, body);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static Task<HttpResponseMessage> PostAsync(AuthClient client, Guid groupId, object body) =>
        SendAsync(client, HttpMethod.Post, $"{GroupsPath}/{groupId}/repayments", body);

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

    private static async Task ShouldHaveErrorsAsync(HttpResponseMessage response, params (string Field, string Code)[] expected)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        errors.EnumerateObject().Select(property => property.Name).ShouldBe(expected.Select(error => error.Field), ignoreOrder: true);
        foreach (var (field, code) in expected)
        {
            errors.GetProperty(field).EnumerateArray().Select(error => error.GetString()).ShouldBe([code]);
        }
    }

    private static async Task<Guid> UserIdAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync("/v1/me", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }
}
