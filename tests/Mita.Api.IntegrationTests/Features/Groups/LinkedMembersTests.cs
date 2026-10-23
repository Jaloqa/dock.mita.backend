using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Groups;

public sealed class LinkedMembersTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string GroupsPath = "/v1/groups";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Should_OpenGroup_ForLinkedUser()
    {
        using var owner = new AuthClient(factory);
        using var linked = new AuthClient(factory);
        var ownerUser = await RegisterAsync(owner);
        var linkedUser = await RegisterAsync(linked);
        var (group, members) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Trip");
        await ShareAsync(owner, group.Id);
        await TestData.LinkMemberAsync(factory, members[1], linkedUser.Id);

        using var response = await linked.Http.GetAsync(GroupPath(group.Id), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync(response);
        body.GetProperty("id").GetGuid().ShouldBe(group.Id);
        body.GetProperty("name").GetString().ShouldBe("Trip");
        body.GetProperty("role").GetString().ShouldBe("member");
        body.GetProperty("myMemberId").GetGuid().ShouldBe(members[1].Id);
        body.GetProperty("shareToken").ValueKind.ShouldBe(JsonValueKind.Null);
        Members(body).Select(member => member.GetProperty("name").GetString()).ShouldBe(["Anna", "Olya", "Max"]);
        Members(body).Select(IsLinked).ShouldBe([false, true, false]);
    }

    [Fact]
    public async Task Should_ShowLinkDetails_ToOwnerOnly()
    {
        using var owner = new AuthClient(factory);
        using var linked = new AuthClient(factory);
        var ownerUser = await RegisterAsync(owner);
        var linkedUser = await RegisterAsync(linked);
        var (group, members) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Trip");
        var token = await ShareAsync(owner, group.Id);
        await TestData.LinkMemberAsync(factory, members[0], ownerUser.Id);
        await TestData.LinkMemberAsync(factory, members[1], linkedUser.Id);
        await TestData.InviteMemberAsync(factory, members[2], "max@example.com");
        using var link = new LinkClient(factory);

        var asOwner = await GetJsonAsync(owner, GroupPath(group.Id));
        var asLinked = await GetJsonAsync(linked, GroupPath(group.Id));
        using var anonymous = await link.GetAsync(token);
        var linkedThroughLink = await GetJsonAsync(linked, LinkClient.PathOf(token));

        asOwner.GetProperty("role").GetString().ShouldBe("owner");
        asOwner.GetProperty("myMemberId").GetGuid().ShouldBe(members[0].Id);
        asOwner.GetProperty("shareToken").GetString().ShouldBe(token);
        Members(asOwner).Select(IsLinked).ShouldBe([true, true, false]);
        Members(asOwner).Select(LinkedNameOf).ShouldBe([ownerUser.Name, linkedUser.Name, null]);
        Members(asOwner).Select(InvitedEmailOf).ShouldBe([null, null, "max@example.com"]);

        asLinked.GetProperty("role").GetString().ShouldBe("member");
        asLinked.GetProperty("myMemberId").GetGuid().ShouldBe(members[1].Id);
        asLinked.GetProperty("shareToken").ValueKind.ShouldBe(JsonValueKind.Null);
        Members(asLinked).Select(IsLinked).ShouldBe([true, true, false]);
        Members(asLinked).Select(LinkedNameOf).ShouldAllBe(name => name == null);
        Members(asLinked).Select(InvitedEmailOf).ShouldAllBe(email => email == null);

        anonymous.StatusCode.ShouldBe(HttpStatusCode.OK);
        var throughLink = await ReadAsync(anonymous);
        throughLink.GetProperty("role").GetString().ShouldBe("link");
        throughLink.GetProperty("myMemberId").ValueKind.ShouldBe(JsonValueKind.Null);
        throughLink.GetProperty("shareToken").ValueKind.ShouldBe(JsonValueKind.Null);
        Members(throughLink).Select(IsLinked).ShouldBe([true, true, false]);
        Members(throughLink).Select(LinkedNameOf).ShouldAllBe(name => name == null);
        Members(throughLink).Select(InvitedEmailOf).ShouldAllBe(email => email == null);
        linkedThroughLink.GetRawText().ShouldBe(throughLink.GetRawText());
    }

    [Fact]
    public async Task Should_RecordLinkedMemberAsActor()
    {
        using var owner = new AuthClient(factory);
        using var linked = new AuthClient(factory);
        var ownerUser = await RegisterAsync(owner);
        var linkedUser = await RegisterAsync(linked);
        var (group, members) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Trip");
        var (anna, olya, max) = (members[0].Id, members[1].Id, members[2].Id);
        await TestData.LinkMemberAsync(factory, members[0], ownerUser.Id);
        await TestData.LinkMemberAsync(factory, members[1], linkedUser.Id);
        var expense = new
        {
            payerId = olya,
            amount = 9_000,
            currency = "EUR",
            date = IsoDay(Today),
            split = new { mode = "equal", memberIds = new[] { anna, olya, max } }
        };

        using var created = await SendAsync(linked, HttpMethod.Post, $"{GroupPath(group.Id)}/expenses", expense);

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdBody = await ReadAsync(created);
        ActorOf(createdBody.GetProperty("createdBy")).ShouldBe(Member(olya, "Olya"));
        createdBody.GetProperty("updatedBy").ValueKind.ShouldBe(JsonValueKind.Null);
        var expenseId = createdBody.GetProperty("id").GetGuid();
        var path = $"{GroupPath(group.Id)}/expenses/{expenseId}";

        using var edited = await SendAsync(linked, HttpMethod.Put, path, expense with { amount = 12_000 });
        using var deleted = await SendAsync(linked, HttpMethod.Delete, path);
        using var restored = await SendAsync(linked, HttpMethod.Post, $"{path}/restore");

        edited.StatusCode.ShouldBe(HttpStatusCode.OK);
        ActorOf((await ReadAsync(edited)).GetProperty("updatedBy")).ShouldBe(Member(olya, "Olya"));
        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        restored.StatusCode.ShouldBe(HttpStatusCode.OK);
        ActorOf((await ReadAsync(restored)).GetProperty("updatedBy")).ShouldBe(Member(olya, "Olya"));

        using var ownerEdit = await SendAsync(owner, HttpMethod.Put, path, expense with { amount = 15_000 });

        ownerEdit.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ownerEditBody = await ReadAsync(ownerEdit);
        ActorOf(ownerEditBody.GetProperty("createdBy")).ShouldBe(Member(olya, "Olya"));
        ActorOf(ownerEditBody.GetProperty("updatedBy")).ShouldBe(Owner(ownerUser.Name));

        var history = await GetJsonAsync(linked, $"{path}/history");

        var events = history.GetProperty("items").EnumerateArray().ToList();
        events.Select(entry => entry.GetProperty("action").GetString())
            .ShouldBe(["updated", "restored", "deleted", "updated", "created"]);
        events.Select(entry => ActorOf(entry.GetProperty("actor")))
            .ShouldBe(
                [
                    Owner(ownerUser.Name), Member(olya, "Olya"), Member(olya, "Olya"), Member(olya, "Olya"),
                    Member(olya, "Olya")
                ]);
        (Guid? UserId, Guid? MemberId)[] stored =
            [(ownerUser.Id, null), (null, olya), (null, olya), (null, olya), (null, olya)];
        (await StoredActorsAsync(expenseId)).ShouldBe(stored);
    }

    [Fact]
    public async Task Should_KeepOwnerRoutes_ForOwnerOnly()
    {
        using var owner = new AuthClient(factory);
        using var linked = new AuthClient(factory);
        var ownerUser = await RegisterAsync(owner);
        var linkedUser = await RegisterAsync(linked);
        var (group, members) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Trip");
        var token = await ShareAsync(owner, group.Id);
        await TestData.LinkMemberAsync(factory, members[1], linkedUser.Id);
        var groupPath = GroupPath(group.Id);
        var refused = new (HttpMethod Method, string Path, object? Body)[]
        {
            (HttpMethod.Put, groupPath, new { name = "Hijacked", currency = "PLN" }),
            (HttpMethod.Delete, groupPath, null),
            (HttpMethod.Post, $"{groupPath}/members", new { name = "Eve" }),
            (HttpMethod.Put, $"{groupPath}/members/{members[1].Id}", new { name = "Eve" }),
            (HttpMethod.Delete, $"{groupPath}/members/{members[2].Id}", null),
            (HttpMethod.Post, $"{groupPath}/share", null),
            (HttpMethod.Delete, $"{groupPath}/share", null)
        };

        foreach (var (method, path, body) in refused)
        {
            using var response = await SendAsync(linked, method, path, body);

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound, $"{method} {path}");
        }

        var stored = await StoredGroupAsync(group.Id);
        stored.Name.ShouldBe("Trip");
        stored.Currency.ShouldBe(Currency.EUR);
        stored.ShareToken.ShouldBe(token);
        stored.Members.ShouldBe(["Anna", "Olya", "Max"]);

        using var category = await SendAsync(linked, HttpMethod.Post, $"{groupPath}/categories", new { name = "Ferry" });

        category.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await StoredGroupAsync(group.Id)).Categories.ShouldBe(["Ferry"]);
    }

    [Fact]
    public async Task Should_ListOwnedAndLinkedGroups()
    {
        using var owner = new AuthClient(factory);
        using var linked = new AuthClient(factory);
        using var inviter = new AuthClient(factory);
        var ownerUser = await RegisterAsync(owner);
        var linkedUser = await RegisterAsync(linked);
        var inviterUser = await RegisterAsync(inviter);
        var (trip, tripMembers) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Trip");
        await TestData.LinkMemberAsync(factory, tripMembers[1], linkedUser.Id);
        var (invited, invitedMembers) = await TestData.AddGroupAsync(factory, inviterUser.Id, "Invited");
        await TestData.InviteMemberAsync(factory, invitedMembers[0], linkedUser.Email);
        var (home, _) = await TestData.AddGroupAsync(factory, linkedUser.Id, "Home", Currency.PLN, "Me", "Flatmate");

        var linkedList = await GetJsonAsync(linked, GroupsPath);
        var ownerList = await GetJsonAsync(owner, GroupsPath);
        var inviterList = await GetJsonAsync(inviter, GroupsPath);

        linkedList.GetProperty("total").GetInt32().ShouldBe(2);
        var items = linkedList.GetProperty("items").EnumerateArray().ToList();
        items.Select(item => item.GetProperty("id").GetGuid()).ShouldBe([home.Id, trip.Id]);
        items.Select(item => item.GetProperty("name").GetString()).ShouldBe(["Home", "Trip"]);
        items.Select(item => item.GetProperty("role").GetString()).ShouldBe(["owner", "member"]);
        items.Select(item => item.GetProperty("members").GetInt32()).ShouldBe([2, 3]);
        ownerList.GetProperty("total").GetInt32().ShouldBe(1);
        ownerList.GetProperty("items").EnumerateArray()
            .Select(item => (item.GetProperty("id").GetGuid(), item.GetProperty("role").GetString()))
            .ShouldBe([(trip.Id, "owner")]);
        inviterList.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())
            .ShouldBe([invited.Id]);
    }

    [Fact]
    public async Task Should_Return404_When_UserLeftOrWasUnlinked()
    {
        using var owner = new AuthClient(factory);
        using var linked = new AuthClient(factory);
        using var stranger = new AuthClient(factory);
        var ownerUser = await RegisterAsync(owner);
        var linkedUser = await RegisterAsync(linked);
        await RegisterAsync(stranger);
        var (group, members) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Trip");
        await TestData.LinkMemberAsync(factory, members[1], linkedUser.Id);
        var expensesPath = $"{GroupPath(group.Id)}/expenses";
        var expense = new { payerId = members[1].Id, amount = 3_000, currency = "EUR", date = IsoDay(Today) };
        (await GetJsonAsync(linked, GroupPath(group.Id))).GetProperty("role").GetString().ShouldBe("member");

        await TestData.UnlinkMemberAsync(factory, members[1]);

        using var read = await linked.Http.GetAsync(GroupPath(group.Id), Ct);
        using var list = await linked.Http.GetAsync(expensesPath, Ct);
        using var write = await SendAsync(linked, HttpMethod.Post, expensesPath, expense);
        using var strangerRead = await stranger.Http.GetAsync(GroupPath(group.Id), Ct);
        using var strangerWrite = await SendAsync(stranger, HttpMethod.Post, expensesPath, expense);

        foreach (var response in new[] { read, list, write, strangerRead, strangerWrite })
        {
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        (await GetJsonAsync(linked, GroupsPath)).GetProperty("total").GetInt32().ShouldBe(0);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.GroupExpenses.CountAsync(row => row.GroupId == group.Id, Ct)).ShouldBe(0);
    }

    private static string GroupPath(Guid groupId) => $"{GroupsPath}/{groupId}";

    private static string IsoDay(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static List<JsonElement> Members(JsonElement group) => [.. group.GetProperty("members").EnumerateArray()];

    private static bool IsLinked(JsonElement member) => member.GetProperty("linked").GetBoolean();

    private static string? LinkedNameOf(JsonElement member) => member.GetProperty("linkedName").GetString();

    private static string? InvitedEmailOf(JsonElement member) => member.GetProperty("invitedEmail").GetString();

    private static (string? Kind, Guid? MemberId, string? Name) Member(Guid memberId, string name) =>
        ("member", memberId, name);

    private static (string? Kind, Guid? MemberId, string? Name) Owner(string name) => ("owner", null, name);

    private static (string? Kind, Guid? MemberId, string? Name) ActorOf(JsonElement actor)
    {
        var memberId = actor.GetProperty("memberId");
        return (
            actor.GetProperty("kind").GetString(),
            memberId.ValueKind == JsonValueKind.Null ? null : memberId.GetGuid(),
            actor.GetProperty("name").GetString());
    }

    private static async Task<RegisteredUser> RegisterAsync(AuthClient client)
    {
        var credentials = await client.RegisterNewAsync(Ct);
        var me = await GetJsonAsync(client, "/v1/me");
        return new RegisteredUser(me.GetProperty("id").GetGuid(), credentials.Name, credentials.Email);
    }

    private static async Task<string> ShareAsync(AuthClient owner, Guid groupId)
    {
        using var response = await SendAsync(owner, HttpMethod.Post, $"{GroupPath(groupId)}/share");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await ReadAsync(response)).GetProperty("shareToken").GetString()!;
    }

    private static async Task<JsonElement> GetJsonAsync(AuthClient client, string path)
    {
        using var response = await client.Http.GetAsync(path, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, path);
        return await ReadAsync(response);
    }

    private static async Task<HttpResponseMessage> SendAsync(
        AuthClient client,
        HttpMethod method,
        string path,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.Http.SendAsync(client.WithXsrf(request), Ct);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    private async Task<(Guid? UserId, Guid? MemberId)[]> StoredActorsAsync(Guid entryId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.GroupEntryEvents.AsNoTracking()
            .Where(entryEvent => entryEvent.EntryId == entryId)
            .OrderByDescending(entryEvent => entryEvent.At)
            .ThenByDescending(entryEvent => entryEvent.Id)
            .Select(entryEvent => new { entryEvent.ActorUserId, entryEvent.ActorMemberId })
            .ToListAsync(Ct);
        return [.. rows.Select(row => (row.ActorUserId, row.ActorMemberId))];
    }

    private async Task<StoredGroup> StoredGroupAsync(Guid groupId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var group = await db.Groups.AsNoTracking().SingleAsync(row => row.Id == groupId, Ct);
        var members = await db.GroupMembers.AsNoTracking()
            .Where(member => member.GroupId == groupId)
            .OrderBy(member => member.Position)
            .Select(member => member.Name)
            .ToArrayAsync(Ct);
        var categories = await db.GroupCategories.AsNoTracking()
            .Where(category => category.GroupId == groupId)
            .Select(category => category.Name)
            .ToArrayAsync(Ct);
        return new StoredGroup(group.Name, group.Currency, group.ShareToken, members, categories);
    }

    private sealed record RegisteredUser(Guid Id, string Name, string Email);

    private sealed record StoredGroup(
        string Name,
        Currency Currency,
        string? ShareToken,
        string[] Members,
        string[] Categories);
}
