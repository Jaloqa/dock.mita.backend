using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Groups;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Groups;

public sealed class GroupRepaymentCoversTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string GroupsPath = "/v1/groups";

    private static readonly string[] OpenShareProperties =
        ["expenseId", "payerId", "date", "description", "categoryId", "share", "open"];

    private static readonly string[] StateCoverProperties = ["expenseId", "groupAmount"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Should_CoverOldestSharesFirst()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var trip = await SetUpAsync(await UserIdAsync(client));
        await TestData.AddGroupExpenseAsync(factory, trip.Group, trip.Max, 3_000, [trip.Anna, trip.Max], IsoDay(Today.AddDays(-6)));

        var first = await CreateAsync(client, trip.Group.Id, Repayment(trip.Anna, trip.Olya, 4_000));

        Covers(first).ShouldBe([(trip.E1, 3_000L), (trip.E2, 1_000L)]);
        var cover = first.GetProperty("covers")[0];
        cover.EnumerateObject().Select(property => property.Name)
            .ShouldBe(["expenseId", "groupAmount", "description", "categoryId"], ignoreOrder: true);
        cover.GetProperty("description").ValueKind.ShouldBe(JsonValueKind.Null);
        cover.GetProperty("categoryId").GetString().ShouldBe(StandardGroupCategories.Default);

        var second = await CreateAsync(client, trip.Group.Id, Repayment(trip.Anna, trip.Olya, 2_000));

        Covers(second).ShouldBe([(trip.E2, 1_000L)]);
        var listed = (await GetAsync(client, $"{GroupsPath}/{trip.Group.Id}/repayments")).GetProperty("items")
            .EnumerateArray()
            .ToDictionary(item => item.GetProperty("id").GetGuid(), Covers);
        listed[first.GetProperty("id").GetGuid()].ShouldBe([(trip.E1, 3_000L), (trip.E2, 1_000L)]);
        listed[second.GetProperty("id").GetGuid()].ShouldBe([(trip.E2, 1_000L)]);
        (await StoredCoversAsync(trip.Group.Id)).ShouldBe(3);
    }

    [Fact]
    public async Task Should_CoverRequestedSharesFirst()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var trip = await SetUpAsync(await UserIdAsync(client));

        var created = await CreateAsync(client, trip.Group.Id, Repayment(trip.Anna, trip.Olya, 4_000, trip.E2));

        Covers(created).ShouldBe([(trip.E2, 2_000L), (trip.E1, 2_000L)]);
        var listed = (await GetAsync(client, $"{GroupsPath}/{trip.Group.Id}/repayments")).GetProperty("items")[0];
        Covers(listed).ShouldBe([(trip.E2, 2_000L), (trip.E1, 2_000L)]);
    }

    [Fact]
    public async Task Should_RefuseSharesThatAreNotOpenBetweenTheMembers()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var trip = await SetUpAsync(userId);
        var (other, otherMembers) = await TestData.AddGroupAsync(factory, userId, "Other");
        var paidByMax = await TestData.AddGroupExpenseAsync(factory, trip.Group, trip.Max, 3_000, trip.Members);
        var withoutAnna = await TestData.AddGroupExpenseAsync(factory, trip.Group, trip.Olya, 3_000, [trip.Olya, trip.Max]);
        var deleted = await TestData.AddGroupExpenseAsync(factory, trip.Group, trip.Olya, 3_000, trip.Members, deleted: true);
        var foreign = await TestData.AddGroupExpenseAsync(factory, other, otherMembers[1], 3_000, otherMembers);

        foreach (var expenseId in new[] { paidByMax.Id, withoutAnna.Id, deleted.Id, foreign.Id, Guid.NewGuid() })
        {
            using var response = await PostAsync(client, trip.Group.Id, Repayment(trip.Anna, trip.Olya, 4_000, trip.E1, expenseId));

            await ShouldHaveErrorsAsync(response, ("settles", "validation.unknownShare"));
        }

        using var both = await PostAsync(
            client,
            trip.Group.Id,
            Repayment(trip.Anna, otherMembers[1], 4_000, trip.E1));

        await ShouldHaveErrorsAsync(both, ("toMemberId", "validation.unknownMember"), ("settles", "validation.unknownShare"));
        (await GetAsync(client, $"{GroupsPath}/{trip.Group.Id}/repayments")).GetProperty("total").GetInt32().ShouldBe(0);
        (await StoredCoversAsync(trip.Group.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task Should_RefuseSettles_When_ShareIsFullyCovered()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var trip = await SetUpAsync(await UserIdAsync(client));
        await CreateAsync(client, trip.Group.Id, Repayment(trip.Anna, trip.Olya, 2_000));
        var partial = await CreateAsync(client, trip.Group.Id, Repayment(trip.Anna, trip.Olya, 1_000, trip.E1));

        Covers(partial).ShouldBe([(trip.E1, 1_000L)]);

        using var refused = await PostAsync(client, trip.Group.Id, Repayment(trip.Anna, trip.Olya, 500, trip.E1));

        await ShouldHaveErrorsAsync(refused, ("settles", "validation.unknownShare"));
        (await GetAsync(client, $"{GroupsPath}/{trip.Group.Id}/repayments")).GetProperty("total").GetInt32().ShouldBe(2);

        var edited = await SendJsonAsync(
            client,
            HttpMethod.Put,
            RepaymentPath(trip.Group.Id, partial.GetProperty("id").GetGuid()),
            Repayment(trip.Anna, trip.Olya, 1_000, trip.E1));

        Covers(edited).ShouldBe([(trip.E1, 1_000L)]);
        (await StoredCoversAsync(trip.Group.Id)).ShouldBe(2);
    }

    [Fact]
    public async Task Should_DropOwnStaleCover_When_EditedAfterCoveredExpenseWasDeleted()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var trip = await SetUpAsync(await UserIdAsync(client));
        var created = await CreateAsync(client, trip.Group.Id, Repayment(trip.Anna, trip.Olya, 2_000, trip.E1));
        var path = RepaymentPath(trip.Group.Id, created.GetProperty("id").GetGuid());
        Covers(created).ShouldBe([(trip.E1, 2_000L)]);
        await DeleteAsync(client, $"{GroupsPath}/{trip.Group.Id}/expenses/{trip.E1}");

        var edited = await SendJsonAsync(
            client,
            HttpMethod.Put,
            path,
            new
            {
                fromMemberId = trip.Anna.Id,
                toMemberId = trip.Olya.Id,
                amount = 2_000,
                currency = "EUR",
                date = IsoDay(Today),
                note = "Cash",
                settles = new[] { trip.E1 }
            });

        Covers(edited).ShouldBe([(trip.E2, 2_000L)]);
        edited.GetProperty("note").GetString().ShouldBe("Cash");
        (await StoredCoversAsync(trip.Group.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task Should_LeaveRepaymentUncovered_When_EditedAfterEveryCoveredShareIsGone()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var trip = await SetUpAsync(await UserIdAsync(client));
        var created = await CreateAsync(client, trip.Group.Id, Repayment(trip.Anna, trip.Olya, 2_000, trip.E1));
        var path = RepaymentPath(trip.Group.Id, created.GetProperty("id").GetGuid());
        await DeleteAsync(client, $"{GroupsPath}/{trip.Group.Id}/expenses/{trip.E1}");
        await DeleteAsync(client, $"{GroupsPath}/{trip.Group.Id}/expenses/{trip.E2}");

        var edited = await SendJsonAsync(client, HttpMethod.Put, path, Repayment(trip.Anna, trip.Olya, 2_000, trip.E1));

        Covers(edited).ShouldBeEmpty();
        (await StoredCoversAsync(trip.Group.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task Should_RefuseSettles_When_EditedWithUnrelatedFullyCoveredShare()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var trip = await SetUpAsync(await UserIdAsync(client));
        var own = await CreateAsync(client, trip.Group.Id, Repayment(trip.Anna, trip.Olya, 1_000, trip.E2));
        await CreateAsync(client, trip.Group.Id, Repayment(trip.Anna, trip.Olya, 3_000, trip.E1));
        var path = RepaymentPath(trip.Group.Id, own.GetProperty("id").GetGuid());

        using var refused = await SendAsync(
            client,
            HttpMethod.Put,
            path,
            Repayment(trip.Anna, trip.Olya, 1_000, trip.E1));

        await ShouldHaveErrorsAsync(refused, ("settles", "validation.unknownShare"));
        var kept = (await GetAsync(client, $"{GroupsPath}/{trip.Group.Id}/repayments")).GetProperty("items")
            .EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == own.GetProperty("id").GetGuid());
        Covers(kept).ShouldBe([(trip.E2, 1_000L)]);
    }

    [Fact]
    public async Task Should_MoveCoverOldestFirst_When_RestoredAfterCoveredExpenseWasDeleted()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var trip = await SetUpAsync(await UserIdAsync(client));
        var created = await CreateAsync(client, trip.Group.Id, Repayment(trip.Anna, trip.Olya, 2_000, trip.E1));
        var path = RepaymentPath(trip.Group.Id, created.GetProperty("id").GetGuid());
        await DeleteAsync(client, path);
        await DeleteAsync(client, $"{GroupsPath}/{trip.Group.Id}/expenses/{trip.E1}");

        var restored = await SendJsonAsync(client, HttpMethod.Post, $"{path}/restore");

        Covers(restored).ShouldBe([(trip.E2, 2_000L)]);
    }

    [Fact]
    public async Task Should_CoverRequestedSharesFirst_When_Edited()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var trip = await SetUpAsync(await UserIdAsync(client));
        var created = await CreateAsync(client, trip.Group.Id, Repayment(trip.Anna, trip.Olya, 4_000));
        var path = RepaymentPath(trip.Group.Id, created.GetProperty("id").GetGuid());

        var edited = await SendJsonAsync(client, HttpMethod.Put, path, Repayment(trip.Anna, trip.Olya, 4_000, trip.E2));

        Covers(edited).ShouldBe([(trip.E2, 2_000L), (trip.E1, 2_000L)]);
        var listed = (await GetAsync(client, $"{GroupsPath}/{trip.Group.Id}/repayments")).GetProperty("items")[0];
        Covers(listed).ShouldBe([(trip.E2, 2_000L), (trip.E1, 2_000L)]);
        var history = (await GetAsync(client, $"{path}/history")).GetProperty("items");
        Covers(history[0].GetProperty("state")).ShouldBe([(trip.E2, 2_000L), (trip.E1, 2_000L)]);
        Covers(history[1].GetProperty("state")).ShouldBe([(trip.E1, 3_000L), (trip.E2, 1_000L)]);
        (await StoredCoversAsync(trip.Group.Id)).ShouldBe(2);
    }

    [Fact]
    public async Task Should_CoverRequestedShares_ThroughLink()
    {
        using var owner = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        var trip = await SetUpAsync(await UserIdAsync(owner));
        var token = await ShareAsync(owner, trip.Group.Id);
        using var link = new LinkClient(factory);
        var asAnna = trip.Anna.Id.ToString();

        using var created = await link.SendAsync(
            HttpMethod.Post,
            token,
            "/repayments",
            Repayment(trip.Anna, trip.Olya, 2_000, trip.E2),
            asAnna);

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Covers(createdBody).ShouldBe([(trip.E2, 2_000L)]);

        using var edited = await link.SendAsync(
            HttpMethod.Put,
            token,
            $"/repayments/{createdBody.GetProperty("id").GetGuid()}",
            Repayment(trip.Anna, trip.Olya, 4_000, trip.E2),
            asAnna);

        edited.StatusCode.ShouldBe(HttpStatusCode.OK);
        Covers(await edited.Content.ReadFromJsonAsync<JsonElement>(Ct)).ShouldBe([(trip.E2, 2_000L), (trip.E1, 2_000L)]);

        using var refused = await link.SendAsync(
            HttpMethod.Post,
            token,
            "/repayments",
            Repayment(trip.Anna, trip.Olya, 1_000, trip.E2),
            asAnna);

        await ShouldHaveErrorsAsync(refused, ("settles", "validation.unknownShare"));
        (await StoredCoversAsync(trip.Group.Id)).ShouldBe(2);
    }

    [Fact]
    public async Task Should_CoverAShareOnce_When_TwoRepaymentsSettleItAtOnce()
    {
        await using var isolated = factory.WithIsolatedData();
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var trip = await SetUpAsync(isolated, await UserIdAsync(client));
        var body = Repayment(trip.Anna, trip.Olya, 3_000, trip.E1);

        var responses = await SendBehindGroupLockAsync(
            isolated,
            trip.Group.Id,
            () => PostAsync(client, trip.Group.Id, body),
            () => PostAsync(client, trip.Group.Id, body));

        using var first = responses[0];
        using var second = responses[1];
        var created = first.StatusCode == HttpStatusCode.Created ? first : second;
        var refused = ReferenceEquals(created, first) ? second : first;
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        Covers(await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).ShouldBe([(trip.E1, 3_000L)]);
        await ShouldHaveErrorsAsync(refused, ("settles", "validation.unknownShare"));
        (await GetAsync(client, $"{GroupsPath}/{trip.Group.Id}/repayments")).GetProperty("total").GetInt32().ShouldBe(1);
        (await StoredCoversAsync(isolated, trip.Group.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task Should_CoverAShareOnce_When_RestoredWhileAnotherRepaymentSettlesIt()
    {
        await using var isolated = factory.WithIsolatedData();
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var trip = await SetUpAsync(isolated, await UserIdAsync(client));
        var removed = await CreateAsync(client, trip.Group.Id, Repayment(trip.Anna, trip.Olya, 3_000));
        var path = RepaymentPath(trip.Group.Id, removed.GetProperty("id").GetGuid());
        await DeleteAsync(client, path);

        var responses = await SendBehindGroupLockAsync(
            isolated,
            trip.Group.Id,
            () => SendAsync(client, HttpMethod.Post, $"{path}/restore"),
            () => PostAsync(client, trip.Group.Id, Repayment(trip.Anna, trip.Olya, 3_000, trip.E1)));

        using var restored = responses[0];
        using var created = responses[1];
        restored.StatusCode.ShouldBe(HttpStatusCode.OK);
        var restoredCovers = Covers(await restored.Content.ReadFromJsonAsync<JsonElement>(Ct));
        if (created.StatusCode == HttpStatusCode.Created)
        {
            Covers(await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).ShouldBe([(trip.E1, 3_000L)]);
            restoredCovers.ShouldBe([(trip.E2, 2_000L)]);
        }
        else
        {
            await ShouldHaveErrorsAsync(created, ("settles", "validation.unknownShare"));
            restoredCovers.ShouldBe([(trip.E1, 3_000L)]);
        }

        (await CoveredPerExpenseAsync(client, trip.Group.Id)).ShouldAllBe(cover => cover.ExpenseId == trip.E2
            ? cover.GroupAmount <= 2_000
            : cover.GroupAmount <= 3_000);
    }

    [Fact]
    public async Task Should_CoverOnlyOpenShares_When_RestoredAfterAnotherRepaymentCoveredThem()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var trip = await SetUpAsync(userId);
        await TestData.LinkMemberAsync(factory, trip.Anna, userId);
        var first = await CreateAsync(client, trip.Group.Id, Repayment(trip.Anna, trip.Olya, 3_000));
        var path = RepaymentPath(trip.Group.Id, first.GetProperty("id").GetGuid());
        Covers(first).ShouldBe([(trip.E1, 3_000L)]);
        await DeleteAsync(client, path);
        var second = await CreateAsync(client, trip.Group.Id, Repayment(trip.Anna, trip.Olya, 3_000));
        Covers(second).ShouldBe([(trip.E1, 3_000L)]);

        var restored = await SendJsonAsync(client, HttpMethod.Post, $"{path}/restore");

        Covers(restored).ShouldBe([(trip.E2, 2_000L)]);
        (await CoveredPerExpenseAsync(client, trip.Group.Id)).ShouldBe([(trip.E1, 3_000L), (trip.E2, 2_000L)], ignoreOrder: true);
        (await GetAsync(client, OpenSharesPath(trip.Group.Id))).GetArrayLength().ShouldBe(0);
        var history = (await GetAsync(client, $"{path}/history")).GetProperty("items").EnumerateArray().ToList();
        history.Select(entry => entry.GetProperty("action").GetString()).ShouldBe(["restored", "deleted", "created"]);
        Covers(history[0].GetProperty("state")).ShouldBe([(trip.E2, 2_000L)]);
        Covers(history[1].GetProperty("state")).ShouldBe([(trip.E1, 3_000L)]);

        await DeleteAsync(client, path);
        var third = await CreateAsync(client, trip.Group.Id, Repayment(trip.Anna, trip.Olya, 2_000));
        Covers(third).ShouldBe([(trip.E2, 2_000L)]);

        var restoredAgain = await SendJsonAsync(client, HttpMethod.Post, $"{path}/restore");

        Covers(restoredAgain).ShouldBeEmpty();
        (await CoveredPerExpenseAsync(client, trip.Group.Id)).ShouldBe([(trip.E1, 3_000L), (trip.E2, 2_000L)], ignoreOrder: true);
        (await StoredCoversAsync(trip.Group.Id)).ShouldBe(2);
    }

    [Fact]
    public async Task Should_KeepCovers_When_RestoredWithNothingTakenMeanwhile()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var trip = await SetUpAsync(await UserIdAsync(client));
        var created = await CreateAsync(client, trip.Group.Id, Repayment(trip.Anna, trip.Olya, 4_000, trip.E2));
        var path = RepaymentPath(trip.Group.Id, created.GetProperty("id").GetGuid());
        await DeleteAsync(client, path);

        var restored = await SendJsonAsync(client, HttpMethod.Post, $"{path}/restore");

        Covers(restored).ShouldBe([(trip.E2, 2_000L), (trip.E1, 2_000L)]);
        var history = (await GetAsync(client, $"{path}/history")).GetProperty("items");
        Covers(history[0].GetProperty("state")).ShouldBe([(trip.E2, 2_000L), (trip.E1, 2_000L)]);
    }

    [Fact]
    public async Task Should_CoverOldestSharesFirst_When_RestoringRepaymentWithoutCovers()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var trip = await SetUpAsync(await UserIdAsync(client));
        var removed = await TestData.AddGroupRepaymentAsync(
            factory, trip.Group, trip.Anna, trip.Olya, 4_000, IsoDay(Today), deleted: true);
        var path = RepaymentPath(trip.Group.Id, removed.Id);

        var restored = await SendJsonAsync(client, HttpMethod.Post, $"{path}/restore");

        Covers(restored).ShouldBe([(trip.E1, 3_000L), (trip.E2, 1_000L)]);
        var history = (await GetAsync(client, $"{path}/history")).GetProperty("items");
        Covers(history[0].GetProperty("state")).ShouldBe([(trip.E1, 3_000L), (trip.E2, 1_000L)]);
        (await StoredCoversAsync(trip.Group.Id)).ShouldBe(2);
    }

    [Fact]
    public async Task Should_RecomputeCovers_OnEditAndRestore_Only()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var trip = await SetUpAsync(userId);
        await TestData.LinkMemberAsync(factory, trip.Anna, userId);
        var created = await CreateAsync(client, trip.Group.Id, Repayment(trip.Anna, trip.Olya, 4_000));
        var path = RepaymentPath(trip.Group.Id, created.GetProperty("id").GetGuid());

        var edited = await SendJsonAsync(client, HttpMethod.Put, path, Repayment(trip.Anna, trip.Olya, 2_000));

        Covers(edited).ShouldBe([(trip.E1, 2_000L)]);
        (await StoredCoversAsync(trip.Group.Id)).ShouldBe(1);

        using (var deleted = await SendAsync(client, HttpMethod.Delete, path))
        {
            deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        OpenShares(await GetAsync(client, OpenSharesPath(trip.Group.Id)))
            .ShouldBe([(trip.E1, 3_000L, 3_000L), (trip.E2, 2_000L, 2_000L)]);

        var restored = await SendJsonAsync(client, HttpMethod.Post, $"{path}/restore");

        Covers(restored).ShouldBe([(trip.E1, 2_000L)]);

        await SendJsonAsync(
            client,
            HttpMethod.Put,
            $"{GroupsPath}/{trip.Group.Id}/expenses/{trip.E1}",
            new
            {
                payerId = trip.Olya.Id,
                amount = 12_000,
                currency = "EUR",
                date = IsoDay(Today.AddDays(-5)),
                categoryId = "groceries",
                description = "Market"
            });

        var listed = (await GetAsync(client, $"{GroupsPath}/{trip.Group.Id}/repayments")).GetProperty("items")[0];
        Covers(listed).ShouldBe([(trip.E1, 2_000L)]);
        listed.GetProperty("covers")[0].GetProperty("description").GetString().ShouldBe("Market");
        listed.GetProperty("covers")[0].GetProperty("categoryId").GetString().ShouldBe("groceries");
        OpenShares(await GetAsync(client, OpenSharesPath(trip.Group.Id)))
            .ShouldBe([(trip.E1, 4_000L, 2_000L), (trip.E2, 2_000L, 2_000L)]);

        var history = (await GetAsync(client, $"{path}/history")).GetProperty("items").EnumerateArray().ToList();

        history.Select(entry => entry.GetProperty("action").GetString()).ShouldBe(["restored", "deleted", "updated", "created"]);
        foreach (var entry in history.Take(3))
        {
            Covers(entry.GetProperty("state")).ShouldBe([(trip.E1, 2_000L)]);
        }

        Covers(history[3].GetProperty("state")).ShouldBe([(trip.E1, 3_000L), (trip.E2, 1_000L)]);
        foreach (var cover in history.SelectMany(entry => entry.GetProperty("state").GetProperty("covers").EnumerateArray()))
        {
            cover.EnumerateObject().Select(property => property.Name).ShouldBe(StateCoverProperties, ignoreOrder: true);
        }
    }

    [Fact]
    public async Task Should_ReadNoCovers_When_StateIsOlderThanCovers()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var trip = await SetUpAsync(await UserIdAsync(client));
        var repayment = await TestData.AddGroupRepaymentAsync(factory, trip.Group, trip.Anna, trip.Olya, 1_000, IsoDay(Today));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlAsync(
                $"""UPDATE group_entry_events SET "State" = "State" - 'covers' WHERE "EntryId" = {repayment.Id}""",
                Ct);
        }

        var history = await GetAsync(client, $"{RepaymentPath(trip.Group.Id, repayment.Id)}/history");

        history.GetProperty("items")[0].GetProperty("state").GetProperty("covers").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Should_ListOpenShares()
    {
        using var owner = new AuthClient(factory);
        using var linked = new AuthClient(factory);
        using var stranger = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        await linked.RegisterNewAsync(Ct);
        await stranger.RegisterNewAsync(Ct);
        var trip = await SetUpAsync(await UserIdAsync(owner));
        await TestData.LinkMemberAsync(factory, trip.Anna, await UserIdAsync(linked));
        await TestData.AddGroupExpenseAsync(factory, trip.Group, trip.Anna, 3_000, trip.Members, IsoDay(Today.AddDays(-4)));

        var before = await GetAsync(linked, OpenSharesPath(trip.Group.Id));

        before.ValueKind.ShouldBe(JsonValueKind.Array);
        OpenShares(before).ShouldBe([(trip.E1, 3_000L, 3_000L), (trip.E2, 2_000L, 2_000L)]);
        var first = before[0];
        first.EnumerateObject().Select(property => property.Name).ShouldBe(OpenShareProperties, ignoreOrder: true);
        first.GetProperty("payerId").GetGuid().ShouldBe(trip.Olya.Id);
        first.GetProperty("date").GetString().ShouldBe(IsoDay(Today.AddDays(-5)));
        first.GetProperty("description").ValueKind.ShouldBe(JsonValueKind.Null);
        first.GetProperty("categoryId").GetString().ShouldBe(StandardGroupCategories.Default);

        await CreateAsync(owner, trip.Group.Id, Repayment(trip.Anna, trip.Olya, 4_000));

        OpenShares(await GetAsync(linked, OpenSharesPath(trip.Group.Id))).ShouldBe([(trip.E2, 2_000L, 1_000L)]);
        (await GetAsync(owner, OpenSharesPath(trip.Group.Id))).GetArrayLength().ShouldBe(0);

        using var foreign = await stranger.Http.GetAsync(OpenSharesPath(trip.Group.Id), Ct);
        using var unknown = await owner.Http.GetAsync(OpenSharesPath(Guid.NewGuid()), Ct);

        foreign.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private Task<Trip> SetUpAsync(Guid ownerId) => SetUpAsync(factory, ownerId);

    private static async Task<Trip> SetUpAsync(WebApplicationFactory<Program> app, Guid ownerId)
    {
        var (group, members) = await TestData.AddGroupAsync(app, ownerId);
        var (anna, olya, max) = (members[0], members[1], members[2]);
        var e1 = await TestData.AddGroupExpenseAsync(app, group, olya, 9_000, members, IsoDay(Today.AddDays(-5)));
        var e2 = await TestData.AddGroupExpenseAsync(app, group, olya, 6_000, members, IsoDay(Today.AddDays(-3)));
        return new Trip(group, members, anna, olya, max, e1.Id, e2.Id);
    }

    private static object Repayment(GroupMember from, GroupMember to, long amount) =>
        new { fromMemberId = from.Id, toMemberId = to.Id, amount, currency = "EUR", date = IsoDay(Today) };

    private static object Repayment(GroupMember from, GroupMember to, long amount, params Guid[] settles) =>
        new { fromMemberId = from.Id, toMemberId = to.Id, amount, currency = "EUR", date = IsoDay(Today), settles };

    private static (Guid ExpenseId, long GroupAmount)[] Covers(JsonElement repayment) =>
    [
        .. repayment.GetProperty("covers").EnumerateArray()
            .Select(cover => (cover.GetProperty("expenseId").GetGuid(), cover.GetProperty("groupAmount").GetInt64()))
    ];

    private static (Guid ExpenseId, long Share, long Open)[] OpenShares(JsonElement shares) =>
    [
        .. shares.EnumerateArray()
            .Select(share => (
                share.GetProperty("expenseId").GetGuid(),
                share.GetProperty("share").GetInt64(),
                share.GetProperty("open").GetInt64()))
    ];

    private static async Task<(Guid ExpenseId, long GroupAmount)[]> CoveredPerExpenseAsync(AuthClient client, Guid groupId)
    {
        var repayments = await GetAsync(client, $"{GroupsPath}/{groupId}/repayments");
        return
        [
            .. repayments.GetProperty("items").EnumerateArray()
                .SelectMany(Covers)
                .GroupBy(cover => cover.ExpenseId, (expenseId, covers) => (expenseId, covers.Sum(cover => cover.GroupAmount)))
        ];
    }

    private Task<int> StoredCoversAsync(Guid groupId) => StoredCoversAsync(factory, groupId);

    private static async Task<int> StoredCoversAsync(WebApplicationFactory<Program> app, Guid groupId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.GroupRepaymentCovers.CountAsync(cover => cover.GroupId == groupId, Ct);
    }

    private static async Task<HttpResponseMessage[]> SendBehindGroupLockAsync(
        WebApplicationFactory<Program> app,
        Guid groupId,
        params Func<Task<HttpResponseMessage>>[] requests)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var holder = await db.Database.BeginTransactionAsync(Ct);
        await db.Database.ExecuteSqlAsync($"""SELECT 1 FROM "groups" WHERE "Id" = {groupId} FOR SHARE""", Ct);

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
            pending.ShouldAllBe(request => !request.IsCompleted, "A request finished while the group row was locked.");
            var blocked = await db.Database
                .SqlQuery<int>($"""SELECT count(*)::int AS "Value" FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock'""")
                .SingleAsync(Ct);
            if (blocked >= pending.Count)
            {
                return;
            }

            await Task.Delay(20, Ct);
        }

        throw new TimeoutException("The requests did not wait for the group row lock.");
    }

    private static async Task<string> ShareAsync(AuthClient owner, Guid groupId)
    {
        using var response = await SendAsync(owner, HttpMethod.Post, $"{GroupsPath}/{groupId}/share");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("shareToken").GetString()!;
    }

    private static async Task DeleteAsync(AuthClient client, string path)
    {
        using var response = await SendAsync(client, HttpMethod.Delete, path);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private static string RepaymentPath(Guid groupId, Guid repaymentId) => $"{GroupsPath}/{groupId}/repayments/{repaymentId}";

    private static string OpenSharesPath(Guid groupId) => $"{GroupsPath}/{groupId}/open-shares";

    private static string IsoDay(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static async Task<JsonElement> CreateAsync(AuthClient client, Guid groupId, object body)
    {
        using var response = await PostAsync(client, groupId, body);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static Task<HttpResponseMessage> PostAsync(AuthClient client, Guid groupId, object body) =>
        SendAsync(client, HttpMethod.Post, $"{GroupsPath}/{groupId}/repayments", body);

    private static async Task<JsonElement> SendJsonAsync(AuthClient client, HttpMethod method, string path, object? body = null)
    {
        using var response = await SendAsync(client, method, path, body);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

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

    private sealed record Trip(
        Group Group,
        GroupMember[] Members,
        GroupMember Anna,
        GroupMember Olya,
        GroupMember Max,
        Guid E1,
        Guid E2);
}
