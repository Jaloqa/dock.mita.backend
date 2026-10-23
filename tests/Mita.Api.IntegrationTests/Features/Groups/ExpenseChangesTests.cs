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

public sealed class ExpenseChangesTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string GroupsPath = "/v1/groups";

    private static readonly string[] StateProperties =
    [
        "payerId", "amount", "currency", "date", "categoryId", "tags", "description", "rate", "rateManual", "groupAmount",
        "split", "shares"
    ];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Should_UpdateExpense_AndRecordTheChange()
    {
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var (group, members) = await TestData.AddGroupAsync(factory, userId);
        var (anna, olya, max) = (members[0].Id, members[1].Id, members[2].Id);
        var created = await CreateAsync(client, group.Id, new { payerId = anna, amount = 9_000, currency = "EUR", date = IsoDay(Today) });

        using var response = await SendAsync(
            client,
            HttpMethod.Put,
            ExpensePath(group.Id, created.GetProperty("id").GetGuid()),
            new { payerId = max, amount = 12_000, currency = "EUR", date = IsoDay(Today) });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("id").GetGuid().ShouldBe(created.GetProperty("id").GetGuid());
        body.GetProperty("amount").GetInt64().ShouldBe(12_000);
        body.GetProperty("groupAmount").GetInt64().ShouldBe(12_000);
        body.GetProperty("payerId").GetGuid().ShouldBe(max);
        Shares(body).ShouldBe([(anna, 4_000L, 4_000L), (olya, 4_000L, 4_000L), (max, 4_000L, 4_000L)]);
        body.GetProperty("deleted").GetBoolean().ShouldBeFalse();
        body.GetProperty("createdBy").GetProperty("kind").GetString().ShouldBe("owner");
        Canonical(body.GetProperty("createdBy")).ShouldBe(Canonical(created.GetProperty("createdBy")));
        body.GetProperty("createdAt").GetString().ShouldBe(created.GetProperty("createdAt").GetString());
        var updatedBy = body.GetProperty("updatedBy");
        updatedBy.GetProperty("kind").GetString().ShouldBe("owner");
        updatedBy.GetProperty("memberId").ValueKind.ShouldBe(JsonValueKind.Null);
        updatedBy.GetProperty("name").GetString().ShouldBe(credentials.Name);
        body.GetProperty("updatedAt").GetString()!.ShouldMatch(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$");

        var history = await GetAsync(client, $"{ExpensePath(group.Id, body.GetProperty("id").GetGuid())}/history");

        history.EnumerateObject().Select(property => property.Name).ShouldBe(["items", "page", "pageSize", "total"], ignoreOrder: true);
        history.GetProperty("page").GetInt32().ShouldBe(1);
        history.GetProperty("pageSize").GetInt32().ShouldBe(100);
        history.GetProperty("total").GetInt32().ShouldBe(2);
        var events = history.GetProperty("items").EnumerateArray().ToList();
        events.Select(entry => entry.GetProperty("action").GetString()).ShouldBe(["updated", "created"]);
        foreach (var entry in events)
        {
            entry.EnumerateObject().Select(property => property.Name).ShouldBe(["id", "action", "at", "actor", "state"], ignoreOrder: true);
            entry.GetProperty("at").GetString()!.ShouldMatch(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$");
            var actor = entry.GetProperty("actor");
            actor.GetProperty("kind").GetString().ShouldBe("owner");
            actor.GetProperty("name").GetString().ShouldBe(credentials.Name);
            entry.GetProperty("state").EnumerateObject().Select(property => property.Name).ShouldBe(StateProperties, ignoreOrder: true);
        }

        var updatedState = events[0].GetProperty("state");
        updatedState.GetProperty("amount").GetInt64().ShouldBe(12_000);
        updatedState.GetProperty("payerId").GetGuid().ShouldBe(max);
        Canonical(updatedState.GetProperty("shares")).ShouldBe(Canonical(body.GetProperty("shares")));
        var createdState = events[1].GetProperty("state");
        createdState.GetProperty("amount").GetInt64().ShouldBe(9_000);
        createdState.GetProperty("payerId").GetGuid().ShouldBe(anna);
        Canonical(createdState.GetProperty("shares")).ShouldBe(Canonical(created.GetProperty("shares")));
    }

    [Fact]
    public async Task Should_KeepStateSharesInMemberOrder_When_SplitGrows()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(client));
        var (anna, olya, max) = (members[0].Id, members[1].Id, members[2].Id);
        var created = await CreateAsync(
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
        var path = ExpensePath(group.Id, created.GetProperty("id").GetGuid());

        using var response = await SendAsync(
            client,
            HttpMethod.Put,
            path,
            new { payerId = max, amount = 9_000, currency = "EUR", date = IsoDay(Today) });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Shares(body).Select(share => share.MemberId).ShouldBe([anna, olya, max]);
        var latest = (await GetAsync(client, $"{path}/history")).GetProperty("items")[0];
        latest.GetProperty("action").GetString().ShouldBe("updated");
        latest.GetProperty("state").GetProperty("shares").EnumerateArray()
            .Select(share => share.GetProperty("memberId").GetGuid())
            .ShouldBe([anna, olya, max]);
    }

    [Fact]
    public async Task Should_RepriceOnEdit_UnlessRateWasManual()
    {
        await using var isolated = factory.WithIsolatedData();
        var (older, newer) = (IsoDay(Today.AddDays(-10)), IsoDay(Today.AddDays(-3)));
        await TestData.AddRatesAsync(
            isolated,
            (Currency.ALL, older, 0.0440m),
            (Currency.ALL, newer, 0.0450m),
            (Currency.EUR, older, 4.30m),
            (Currency.EUR, newer, 4.30m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(isolated, await UserIdAsync(client));
        var olya = members[1].Id;
        var created = await CreateAsync(client, group.Id, new { payerId = olya, amount = 300_000, currency = "ALL", date = older });
        created.GetProperty("groupAmount").GetInt64().ShouldBe(3_070);
        var path = ExpensePath(group.Id, created.GetProperty("id").GetGuid());

        using var repriced = await SendAsync(
            client,
            HttpMethod.Put,
            path,
            new { payerId = olya, amount = 300_000, currency = "ALL", date = newer });

        repriced.StatusCode.ShouldBe(HttpStatusCode.OK);
        var repricedBody = await repriced.Content.ReadFromJsonAsync<JsonElement>(Ct);
        repricedBody.GetProperty("rate").GetDouble().ShouldBe(0.0104651);
        repricedBody.GetProperty("groupAmount").GetInt64().ShouldBe(3_140);
        repricedBody.GetProperty("rateManual").GetBoolean().ShouldBeFalse();
        Shares(repricedBody).Sum(share => share.GroupAmount).ShouldBe(3_140);

        using var manual = await SendAsync(
            client,
            HttpMethod.Put,
            path,
            new { payerId = olya, amount = 300_000, currency = "ALL", date = newer, rate = 0.0105 });

        manual.StatusCode.ShouldBe(HttpStatusCode.OK);
        var manualBody = await manual.Content.ReadFromJsonAsync<JsonElement>(Ct);
        manualBody.GetProperty("rate").GetDouble().ShouldBe(0.0105);
        manualBody.GetProperty("groupAmount").GetInt64().ShouldBe(3_150);
        manualBody.GetProperty("rateManual").GetBoolean().ShouldBeTrue();
        Shares(manualBody).Sum(share => share.GroupAmount).ShouldBe(3_150);
    }

    [Fact]
    public async Task Should_DeleteAndRestoreExpense()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(client));
        var created = await CreateAsync(
            client,
            group.Id,
            new { payerId = members[1].Id, amount = 10_000, currency = "EUR", date = IsoDay(Today) });
        var expenseId = created.GetProperty("id").GetGuid();
        var path = ExpensePath(group.Id, expenseId);
        var body = new { payerId = members[1].Id, amount = 10_000, currency = "EUR", date = IsoDay(Today) };

        using var deleted = await SendAsync(client, HttpMethod.Delete, path);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetAsync(client, $"{GroupsPath}/{group.Id}/expenses")).GetProperty("total").GetInt32().ShouldBe(0);
        var removed = await GetAsync(client, $"{GroupsPath}/{group.Id}/expenses?deleted=true");
        removed.GetProperty("total").GetInt32().ShouldBe(1);
        var removedItem = removed.GetProperty("items")[0];
        removedItem.GetProperty("id").GetGuid().ShouldBe(expenseId);
        removedItem.GetProperty("deleted").GetBoolean().ShouldBeTrue();
        removedItem.GetProperty("updatedBy").GetProperty("kind").GetString().ShouldBe("owner");

        using var editDeleted = await SendAsync(client, HttpMethod.Put, path, body);
        using var deleteAgain = await SendAsync(client, HttpMethod.Delete, path);

        editDeleted.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        deleteAgain.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var restored = await SendAsync(client, HttpMethod.Post, $"{path}/restore");

        restored.StatusCode.ShouldBe(HttpStatusCode.OK);
        var restoredBody = await restored.Content.ReadFromJsonAsync<JsonElement>(Ct);
        restoredBody.GetProperty("id").GetGuid().ShouldBe(expenseId);
        restoredBody.GetProperty("deleted").GetBoolean().ShouldBeFalse();
        restoredBody.GetProperty("amount").GetInt64().ShouldBe(10_000);
        Canonical(restoredBody.GetProperty("shares")).ShouldBe(Canonical(created.GetProperty("shares")));
        (await GetAsync(client, $"{GroupsPath}/{group.Id}/expenses")).GetProperty("total").GetInt32().ShouldBe(1);

        using var restoreAgain = await SendAsync(client, HttpMethod.Post, $"{path}/restore");

        restoreAgain.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var history = await GetAsync(client, $"{path}/history");

        history.GetProperty("total").GetInt32().ShouldBe(3);
        history.GetProperty("items").EnumerateArray().Select(entry => entry.GetProperty("action").GetString())
            .ShouldBe(["restored", "deleted", "created"]);
        history.GetProperty("items").EnumerateArray()
            .ShouldAllBe(entry => entry.GetProperty("state").GetProperty("amount").GetInt64() == 10_000);
    }

    [Fact]
    public async Task Should_ExcludeDeletedExpenses_FromTotal()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(client));
        await CreateAsync(client, group.Id, new { payerId = members[0].Id, amount = 9_000, currency = "EUR", date = IsoDay(Today) });
        var small = await CreateAsync(
            client,
            group.Id,
            new { payerId = members[1].Id, amount = 3_000, currency = "EUR", date = IsoDay(Today) });
        var path = ExpensePath(group.Id, small.GetProperty("id").GetGuid());
        (await TotalAsync(client, group.Id)).ShouldBe(12_000);

        using var deleted = await SendAsync(client, HttpMethod.Delete, path);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await TotalAsync(client, group.Id)).ShouldBe(9_000);

        using var restored = await SendAsync(client, HttpMethod.Post, $"{path}/restore");

        restored.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await TotalAsync(client, group.Id)).ShouldBe(12_000);
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
        var live = await TestData.AddGroupExpenseAsync(factory, groupX, membersX[0], 1_000, membersX, IsoDay(Today));
        var removed = await TestData.AddGroupExpenseAsync(factory, groupX, membersX[0], 2_000, membersX, IsoDay(Today), deleted: true);
        var liveBody = new { payerId = membersY[0].Id, amount = 1_000, currency = "EUR", date = IsoDay(Today) };
        var foreignBody = new { payerId = membersX[0].Id, amount = 1_000, currency = "EUR", date = IsoDay(Today) };

        var underOtherGroup = new[]
        {
            await SendAsync(owner, HttpMethod.Put, ExpensePath(groupY.Id, live.Id), liveBody),
            await SendAsync(owner, HttpMethod.Delete, ExpensePath(groupY.Id, live.Id)),
            await SendAsync(owner, HttpMethod.Post, $"{ExpensePath(groupY.Id, removed.Id)}/restore"),
            await SendAsync(owner, HttpMethod.Get, $"{ExpensePath(groupY.Id, live.Id)}/history"),
            await SendAsync(owner, HttpMethod.Get, $"{ExpensePath(groupY.Id, removed.Id)}/history")
        };
        var byIntruder = new[]
        {
            await SendAsync(intruder, HttpMethod.Put, ExpensePath(groupX.Id, live.Id), foreignBody),
            await SendAsync(intruder, HttpMethod.Delete, ExpensePath(groupX.Id, live.Id)),
            await SendAsync(intruder, HttpMethod.Post, $"{ExpensePath(groupX.Id, removed.Id)}/restore"),
            await SendAsync(intruder, HttpMethod.Get, $"{ExpensePath(groupX.Id, live.Id)}/history")
        };
        var unknown = new[]
        {
            await SendAsync(owner, HttpMethod.Put, ExpensePath(groupX.Id, Guid.NewGuid()), foreignBody),
            await SendAsync(owner, HttpMethod.Delete, ExpensePath(groupX.Id, Guid.NewGuid())),
            await SendAsync(owner, HttpMethod.Post, $"{ExpensePath(groupX.Id, Guid.NewGuid())}/restore"),
            await SendAsync(owner, HttpMethod.Get, $"{ExpensePath(groupX.Id, Guid.NewGuid())}/history"),
            await SendAsync(owner, HttpMethod.Delete, $"{GroupsPath}/{groupX.Id}/expenses/not-a-guid")
        };

        foreach (var response in underOtherGroup.Concat(byIntruder).Concat(unknown))
        {
            using (response)
            {
                response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            }
        }

        var untouched = await GetAsync(owner, $"{GroupsPath}/{groupX.Id}/expenses");
        untouched.GetProperty("total").GetInt32().ShouldBe(1);
        untouched.GetProperty("items")[0].GetProperty("updatedBy").ValueKind.ShouldBe(JsonValueKind.Null);
        (await GetAsync(owner, $"{GroupsPath}/{groupX.Id}/expenses?deleted=true")).GetProperty("total").GetInt32().ShouldBe(1);

        using var invalid = await SendAsync(
            owner,
            HttpMethod.Put,
            ExpensePath(groupX.Id, Guid.NewGuid()),
            new { amount = 1_000, currency = "EUR", date = IsoDay(Today) });

        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await invalid.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        errors.EnumerateObject().Select(property => property.Name).ShouldBe(["payerId"]);
        errors.GetProperty("payerId").EnumerateArray().Select(error => error.GetString()).ShouldBe(["validation.required"]);
    }

    [Fact]
    public async Task Should_RefuseUnknownReferences_When_ExpenseIsEdited()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var (group, members) = await TestData.AddGroupAsync(factory, userId, "Trip");
        var (_, otherMembers) = await TestData.AddGroupAsync(factory, userId, "Other");
        var created = await CreateAsync(client, group.Id, new { payerId = members[0].Id, amount = 9_000, currency = "EUR", date = IsoDay(Today) });
        var path = ExpensePath(group.Id, created.GetProperty("id").GetGuid());

        using var response = await SendAsync(
            client,
            HttpMethod.Put,
            path,
            new { payerId = otherMembers[0].Id, amount = 12_000, currency = "EUR", date = IsoDay(Today) });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        errors.GetProperty("payerId").EnumerateArray().Select(error => error.GetString()).ShouldBe(["validation.unknownMember"]);
        var unchanged = (await GetAsync(client, $"{GroupsPath}/{group.Id}/expenses")).GetProperty("items")[0];
        unchanged.GetProperty("amount").GetInt64().ShouldBe(9_000);
        unchanged.GetProperty("updatedBy").ValueKind.ShouldBe(JsonValueKind.Null);
        (await GetAsync(client, $"{path}/history")).GetProperty("total").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Should_ListSameDayExpenses_NewestCreatedFirst()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(client));
        var day = IsoDay(Today.AddDays(-1));
        var first = await CreateAsync(client, group.Id, new { payerId = members[0].Id, amount = 1_000, currency = "EUR", date = day });
        var second = await CreateAsync(client, group.Id, new { payerId = members[1].Id, amount = 2_000, currency = "EUR", date = day });
        var earlierDay = await CreateAsync(
            client,
            group.Id,
            new { payerId = members[2].Id, amount = 3_000, currency = "EUR", date = IsoDay(Today.AddDays(-2)) });

        var listed = await GetAsync(client, $"{GroupsPath}/{group.Id}/expenses");

        listed.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ShouldBe(
        [
            second.GetProperty("id").GetGuid(),
            first.GetProperty("id").GetGuid(),
            earlierDay.GetProperty("id").GetGuid()
        ]);
    }

    [Fact]
    public async Task Should_KeepHistoryConsistent_When_ExpenseEditedConcurrently()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(client));
        var (anna, max) = (members[0].Id, members[2].Id);

        for (var round = 0; round < 10; round++)
        {
            var created = await CreateAsync(client, group.Id, new { payerId = anna, amount = 9_000, currency = "EUR", date = IsoDay(Today) });
            var path = ExpensePath(group.Id, created.GetProperty("id").GetGuid());
            var first = SendAsync(
                client,
                HttpMethod.Put,
                path,
                new { payerId = max, amount = 12_000, currency = "EUR", date = IsoDay(Today) });
            var second = SendAsync(
                client,
                HttpMethod.Put,
                path,
                new { payerId = anna, amount = 15_000, currency = "EUR", date = IsoDay(Today) });
            await Task.WhenAll(first, second);
            using var firstResponse = await first;
            using var secondResponse = await second;

            firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            var history = (await GetAsync(client, $"{path}/history")).GetProperty("items").EnumerateArray().ToList();
            history.Select(entry => entry.GetProperty("action").GetString()).ShouldBe(["updated", "updated", "created"]);
            var stored = (await GetAsync(client, $"{GroupsPath}/{group.Id}/expenses")).GetProperty("items").EnumerateArray()
                .Single(item => item.GetProperty("id").GetGuid() == created.GetProperty("id").GetGuid());
            var latest = history[0].GetProperty("state");
            latest.GetProperty("payerId").GetGuid().ShouldBe(stored.GetProperty("payerId").GetGuid());
            latest.GetProperty("amount").GetInt64().ShouldBe(stored.GetProperty("amount").GetInt64());
            Canonical(latest.GetProperty("shares")).ShouldBe(Canonical(stored.GetProperty("shares")));
            Shares(stored).Sum(share => share.Amount).ShouldBe(stored.GetProperty("amount").GetInt64());
        }
    }

    [Fact]
    public async Task Should_NotEditDeletedExpense_When_DeletedConcurrently()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(client));

        for (var round = 0; round < 10; round++)
        {
            var created = await CreateAsync(
                client,
                group.Id,
                new { payerId = members[0].Id, amount = 9_000, currency = "EUR", date = IsoDay(Today) });
            var path = ExpensePath(group.Id, created.GetProperty("id").GetGuid());
            var edit = SendAsync(
                client,
                HttpMethod.Put,
                path,
                new { payerId = members[2].Id, amount = 12_000, currency = "EUR", date = IsoDay(Today) });
            var delete = SendAsync(client, HttpMethod.Delete, path);
            await Task.WhenAll(edit, delete);
            using var edited = await edit;
            using var deleted = await delete;

            deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            var history = (await GetAsync(client, $"{path}/history")).GetProperty("items").EnumerateArray().ToList();
            var actions = history.Select(entry => entry.GetProperty("action").GetString()).ToList();
            if (edited.StatusCode == HttpStatusCode.OK)
            {
                actions.ShouldBe(["deleted", "updated", "created"]);
            }
            else
            {
                edited.StatusCode.ShouldBe(HttpStatusCode.NotFound);
                actions.ShouldBe(["deleted", "created"]);
            }

            var stored = (await GetAsync(client, $"{GroupsPath}/{group.Id}/expenses?deleted=true")).GetProperty("items")
                .EnumerateArray()
                .Single(item => item.GetProperty("id").GetGuid() == created.GetProperty("id").GetGuid());
            var deletedState = history[0].GetProperty("state");
            deletedState.GetProperty("amount").GetInt64().ShouldBe(stored.GetProperty("amount").GetInt64());
            Canonical(deletedState.GetProperty("shares")).ShouldBe(Canonical(stored.GetProperty("shares")));
        }
    }

    [Fact]
    public Task Should_ApplyLatestSplit_When_EditWaitsForEditThatShrankTheSplit() =>
        RunEditRaceAsync(initial: [0, 1, 2], first: [0, 2], second: [0, 1, 2]);

    [Fact]
    public Task Should_ApplyLatestSplit_When_EditWaitsForEditThatGrewTheSplit() =>
        RunEditRaceAsync(initial: [0, 2], first: [0, 1, 2], second: [0, 2]);

    [Fact]
    public async Task Should_RecordStoredShares_When_DeleteWaitsForEdit()
    {
        await using var isolated = factory.WithIsolatedData();
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(isolated, await UserIdAsync(client));
        var created = await CreateAsync(client, group.Id, EqualExpense(members, [0, 1, 2], 9_000));
        var expenseId = created.GetProperty("id").GetGuid();
        var path = ExpensePath(group.Id, expenseId);

        var responses = await SendBehindExpenseLockAsync(
            isolated,
            expenseId,
            () => SendAsync(client, HttpMethod.Put, path, EqualExpense(members, [0, 2], 12_000)),
            () => SendAsync(client, HttpMethod.Delete, path));

        using var edited = responses[0];
        using var deleted = responses[1];
        edited.StatusCode.ShouldBe(HttpStatusCode.OK);
        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var stored = (await GetAsync(client, $"{GroupsPath}/{group.Id}/expenses?deleted=true")).GetProperty("items")
            .EnumerateArray()
            .Single();
        stored.GetProperty("amount").GetInt64().ShouldBe(12_000);
        Shares(stored).Select(share => share.MemberId).ShouldBe([members[0].Id, members[2].Id], ignoreOrder: true);
        var history = (await GetAsync(client, $"{path}/history")).GetProperty("items").EnumerateArray().ToList();
        history.Select(entry => entry.GetProperty("action").GetString()).ShouldBe(["deleted", "updated", "created"]);
        foreach (var entry in history.Take(2))
        {
            var state = entry.GetProperty("state");
            state.GetProperty("amount").GetInt64().ShouldBe(12_000);
            state.GetProperty("payerId").GetGuid().ShouldBe(stored.GetProperty("payerId").GetGuid());
            Canonical(state.GetProperty("shares")).ShouldBe(Canonical(stored.GetProperty("shares")));
        }
    }

    private async Task RunEditRaceAsync(int[] initial, int[] first, int[] second)
    {
        await using var isolated = factory.WithIsolatedData();
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(isolated, await UserIdAsync(client));
        var created = await CreateAsync(client, group.Id, EqualExpense(members, initial, 9_000));
        var expenseId = created.GetProperty("id").GetGuid();
        var path = ExpensePath(group.Id, expenseId);

        var responses = await SendBehindExpenseLockAsync(
            isolated,
            expenseId,
            () => SendAsync(client, HttpMethod.Put, path, EqualExpense(members, first, 12_000)),
            () => SendAsync(client, HttpMethod.Put, path, EqualExpense(members, second, 15_000)));

        foreach (var response in responses)
        {
            using (response)
            {
                response.StatusCode.ShouldBe(HttpStatusCode.OK);
            }
        }

        var stored = (await GetAsync(client, $"{GroupsPath}/{group.Id}/expenses")).GetProperty("items").EnumerateArray().Single();
        stored.GetProperty("amount").GetInt64().ShouldBe(15_000);
        Shares(stored).Select(share => share.MemberId).ShouldBe(second.Select(index => members[index].Id), ignoreOrder: true);
        Shares(stored).Sum(share => share.Amount).ShouldBe(15_000);
        Shares(stored).Sum(share => share.GroupAmount).ShouldBe(15_000);
        var history = (await GetAsync(client, $"{path}/history")).GetProperty("items").EnumerateArray().ToList();
        history.Select(entry => entry.GetProperty("action").GetString()).ShouldBe(["updated", "updated", "created"]);
        var latest = history[0].GetProperty("state");
        latest.GetProperty("amount").GetInt64().ShouldBe(15_000);
        Canonical(latest.GetProperty("shares")).ShouldBe(Canonical(stored.GetProperty("shares")));
    }

    private static object EqualExpense(GroupMember[] members, int[] between, long amount) => new
    {
        payerId = members[between[0]].Id,
        amount,
        currency = "EUR",
        date = IsoDay(Today),
        split = new { mode = "equal", memberIds = between.Select(index => members[index].Id).ToArray() }
    };

    private static async Task<HttpResponseMessage[]> SendBehindExpenseLockAsync(
        WebApplicationFactory<Program> app,
        Guid expenseId,
        params Func<Task<HttpResponseMessage>>[] requests)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var holder = await db.Database.BeginTransactionAsync(Ct);
        await db.Database.ExecuteSqlAsync($"""SELECT 1 FROM group_expenses WHERE "Id" = {expenseId} FOR UPDATE""", Ct);

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
            pending.ShouldAllBe(request => !request.IsCompleted, "A request finished while the expense row was locked.");
            var blocked = await db.Database
                .SqlQuery<int>($"""SELECT count(*)::int AS "Value" FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock'""")
                .SingleAsync(Ct);
            if (blocked >= pending.Count)
            {
                return;
            }

            await Task.Delay(20, Ct);
        }

        throw new TimeoutException("The requests did not wait for the expense row lock.");
    }

    private static (Guid MemberId, long Amount, long GroupAmount)[] Shares(JsonElement expense) =>
    [
        .. expense.GetProperty("shares").EnumerateArray().Select(share => (
            share.GetProperty("memberId").GetGuid(),
            share.GetProperty("amount").GetInt64(),
            share.GetProperty("groupAmount").GetInt64()))
    ];

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

    private static string ExpensePath(Guid groupId, Guid expenseId) => $"{GroupsPath}/{groupId}/expenses/{expenseId}";

    private static string IsoDay(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static async Task<JsonElement> CreateAsync(AuthClient client, Guid groupId, object body)
    {
        using var response = await SendAsync(client, HttpMethod.Post, $"{GroupsPath}/{groupId}/expenses", body);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task<long> TotalAsync(AuthClient client, Guid groupId) =>
        (await GetAsync(client, $"{GroupsPath}/{groupId}")).GetProperty("total").GetInt64();

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

    private static async Task<Guid> UserIdAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync("/v1/me", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }
}
