using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Groups;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Groups;

public sealed class MemberLinksTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string GroupsPath = "/v1/groups";

    private static readonly string[] MemberProperties = ["id", "name", "inUse", "linked", "linkedName", "invitedEmail"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_LinkOwner_ToMember()
    {
        using var owner = new AuthClient(factory);
        var ownerUser = await RegisterAsync(owner);
        var (group, members) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Trip");
        var (anna, olya) = (members[0], members[1]);

        using var linked = await SendAsync(owner, HttpMethod.Put, LinkPath(group.Id, anna.Id));

        linked.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync(linked);
        body.EnumerateObject().Select(property => property.Name).ShouldBe(MemberProperties);
        body.GetProperty("id").GetGuid().ShouldBe(anna.Id);
        body.GetProperty("name").GetString().ShouldBe("Anna");
        body.GetProperty("inUse").GetBoolean().ShouldBeFalse();
        body.GetProperty("linked").GetBoolean().ShouldBeTrue();
        body.GetProperty("linkedName").GetString().ShouldBe(ownerUser.Name);
        body.GetProperty("invitedEmail").ValueKind.ShouldBe(JsonValueKind.Null);

        using var again = await SendAsync(owner, HttpMethod.Put, LinkPath(group.Id, anna.Id));

        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync(again)).GetRawText().ShouldBe(body.GetRawText());
        var read = await GetJsonAsync(owner, GroupPath(group.Id));
        read.GetProperty("myMemberId").GetGuid().ShouldBe(anna.Id);
        Members(read)[0].GetRawText().ShouldBe(body.GetRawText());

        using var second = await SendAsync(owner, HttpMethod.Put, LinkPath(group.Id, olya.Id));

        await ShouldHaveErrorsAsync(second, ("memberId", "validation.alreadyLinked"));
        (await StoredMembersAsync(group.Id)).Select(member => member.UserId).ShouldBe([ownerUser.Id, null, null]);
    }

    [Fact]
    public async Task Should_RefuseLink_When_MemberLinkedToSomeoneElse()
    {
        using var owner = new AuthClient(factory);
        using var other = new AuthClient(factory);
        var ownerUser = await RegisterAsync(owner);
        var otherUser = await RegisterAsync(other);
        var (group, members) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Trip");
        await TestData.LinkMemberAsync(factory, members[1], otherUser.Id);

        using var refused = await SendAsync(owner, HttpMethod.Put, LinkPath(group.Id, members[1].Id));

        await ShouldHaveErrorsAsync(refused, ("memberId", "validation.memberLinked"));
        (await GetJsonAsync(owner, GroupPath(group.Id))).GetProperty("myMemberId").ValueKind
            .ShouldBe(JsonValueKind.Null);

        await TestData.LinkMemberAsync(factory, members[0], ownerUser.Id);

        using var bothFail = await SendAsync(owner, HttpMethod.Put, LinkPath(group.Id, members[1].Id));

        await ShouldHaveErrorsAsync(bothFail, ("memberId", "validation.memberLinked"));
        (await StoredMembersAsync(group.Id)).Select(member => member.UserId).ShouldBe([ownerUser.Id, otherUser.Id, null]);
    }

    [Fact]
    public async Task Should_UnlinkMember()
    {
        using var owner = new AuthClient(factory);
        using var other = new AuthClient(factory);
        var ownerUser = await RegisterAsync(owner);
        var otherUser = await RegisterAsync(other);
        var (group, members) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Trip");
        await TestData.LinkMemberAsync(factory, members[1], otherUser.Id);
        (await GetJsonAsync(other, GroupPath(group.Id))).GetProperty("role").GetString().ShouldBe("member");

        using var unlinked = await SendAsync(owner, HttpMethod.Delete, LinkPath(group.Id, members[1].Id));

        unlinked.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await unlinked.Content.ReadAsStringAsync(Ct)).ShouldBeEmpty();
        using var lostAccess = await other.Http.GetAsync(GroupPath(group.Id), Ct);
        lostAccess.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await StoredMembersAsync(group.Id)).Select(member => member.UserId).ShouldAllBe(userId => userId == null);
        Members(await GetJsonAsync(owner, GroupPath(group.Id))).Select(member => member.GetProperty("linked").GetBoolean())
            .ShouldBe([false, false, false]);

        using var again = await SendAsync(owner, HttpMethod.Delete, LinkPath(group.Id, members[1].Id));

        again.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await TestData.LinkMemberAsync(factory, members[0], ownerUser.Id);

        using var self = await SendAsync(owner, HttpMethod.Delete, LinkPath(group.Id, members[0].Id));

        self.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetJsonAsync(owner, GroupPath(group.Id))).GetProperty("myMemberId").ValueKind
            .ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Should_InviteMember()
    {
        using var owner = new AuthClient(factory);
        var ownerUser = await RegisterAsync(owner);
        var (group, members) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Trip");
        var max = members[2];
        var before = DateTimeOffset.UtcNow;

        using var invited = await SendAsync(owner, HttpMethod.Put, InvitePath(group.Id, max.Id), new { email = " Max@Example.com " });

        invited.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync(invited);
        body.EnumerateObject().Select(property => property.Name).ShouldBe(MemberProperties);
        body.GetProperty("id").GetGuid().ShouldBe(max.Id);
        body.GetProperty("name").GetString().ShouldBe("Max");
        body.GetProperty("inUse").GetBoolean().ShouldBeFalse();
        body.GetProperty("linked").GetBoolean().ShouldBeFalse();
        body.GetProperty("linkedName").ValueKind.ShouldBe(JsonValueKind.Null);
        body.GetProperty("invitedEmail").GetString().ShouldBe("max@example.com");
        var first = (await StoredMembersAsync(group.Id))[2];
        first.InvitedEmail.ShouldBe("max@example.com");
        first.InvitedAt.ShouldNotBeNull();
        first.InvitedAt.Value.ShouldBeGreaterThanOrEqualTo(before.AddSeconds(-5));
        first.InvitedAt.Value.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow.AddSeconds(5));
        Members(await GetJsonAsync(owner, GroupPath(group.Id)))[2].GetRawText().ShouldBe(body.GetRawText());

        using var replaced = await SendAsync(owner, HttpMethod.Put, InvitePath(group.Id, max.Id), new { email = "m@x.pl" });

        replaced.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync(replaced)).GetProperty("invitedEmail").GetString().ShouldBe("m@x.pl");
        var second = (await StoredMembersAsync(group.Id))[2];
        second.InvitedEmail.ShouldBe("m@x.pl");
        second.InvitedAt.ShouldNotBeNull();
        second.InvitedAt.Value.ShouldBeGreaterThanOrEqualTo(first.InvitedAt.Value);
        Members(await GetJsonAsync(owner, GroupPath(group.Id))).Select(InvitedEmailOf).ShouldBe([null, null, "m@x.pl"]);

        using var cancelled = await SendAsync(owner, HttpMethod.Delete, InvitePath(group.Id, max.Id));

        cancelled.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await cancelled.Content.ReadAsStringAsync(Ct)).ShouldBeEmpty();
        Members(await GetJsonAsync(owner, GroupPath(group.Id))).Select(InvitedEmailOf).ShouldAllBe(email => email == null);
        var stored = (await StoredMembersAsync(group.Id))[2];
        stored.InvitedEmail.ShouldBeNull();
        stored.InvitedAt.ShouldBeNull();

        using var again = await SendAsync(owner, HttpMethod.Delete, InvitePath(group.Id, max.Id));

        again.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Should_RefuseInvites()
    {
        using var owner = new AuthClient(factory);
        using var other = new AuthClient(factory);
        var ownerUser = await RegisterAsync(owner);
        var otherUser = await RegisterAsync(other);
        var (group, members) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Trip");
        var (anna, olya, max) = (members[0], members[1], members[2]);

        using var self = await SendAsync(
            owner,
            HttpMethod.Put,
            InvitePath(group.Id, anna.Id),
            new { email = $" {ownerUser.Email.ToUpperInvariant()} " });

        await ShouldHaveErrorsAsync(self, ("email", "validation.inviteSelf"));

        using var olyaInvited = await SendAsync(owner, HttpMethod.Put, InvitePath(group.Id, olya.Id), new { email = "o@x.pl" });
        using var taken = await SendAsync(owner, HttpMethod.Put, InvitePath(group.Id, max.Id), new { email = "O@X.pl" });

        olyaInvited.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ShouldHaveErrorsAsync(taken, ("email", "validation.alreadyInvited"));

        using var reinvited = await SendAsync(owner, HttpMethod.Put, InvitePath(group.Id, olya.Id), new { email = "O@x.PL" });

        reinvited.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StoredMembersAsync(group.Id)).Select(member => member.InvitedEmail).ShouldBe([null, "o@x.pl", null]);

        await TestData.LinkMemberAsync(factory, olya, otherUser.Id);

        using var linked = await SendAsync(owner, HttpMethod.Put, InvitePath(group.Id, olya.Id), new { email = "new@x.pl" });

        await ShouldHaveErrorsAsync(linked, ("memberId", "validation.memberLinked"));

        using var maxInvited = await SendAsync(owner, HttpMethod.Put, InvitePath(group.Id, max.Id), new { email = "m@x.pl" });
        using var both = await SendAsync(owner, HttpMethod.Put, InvitePath(group.Id, olya.Id), new { email = "M@x.pl" });

        maxInvited.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ShouldHaveErrorsAsync(both, ("memberId", "validation.memberLinked"), ("email", "validation.alreadyInvited"));

        using var linkedSelf = await SendAsync(owner, HttpMethod.Put, InvitePath(group.Id, olya.Id), new { email = ownerUser.Email });

        await ShouldHaveErrorsAsync(linkedSelf, ("memberId", "validation.memberLinked"), ("email", "validation.inviteSelf"));
        var stored = await StoredMembersAsync(group.Id);
        stored.Select(member => member.UserId).ShouldBe([null, otherUser.Id, null]);
        stored.Select(member => member.InvitedEmail).ShouldBe([null, null, "m@x.pl"]);
    }

    [Fact]
    public async Task Should_ClearInvite_When_MemberLinked()
    {
        using var owner = new AuthClient(factory);
        var ownerUser = await RegisterAsync(owner);
        var (group, members) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Trip");
        var anna = members[0];
        using var invited = await SendAsync(owner, HttpMethod.Put, InvitePath(group.Id, anna.Id), new { email = "anna@example.com" });
        invited.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var linked = await SendAsync(owner, HttpMethod.Put, LinkPath(group.Id, anna.Id));

        linked.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadAsync(linked);
        body.GetProperty("linked").GetBoolean().ShouldBeTrue();
        body.GetProperty("invitedEmail").ValueKind.ShouldBe(JsonValueKind.Null);
        var stored = (await StoredMembersAsync(group.Id))[0];
        stored.UserId.ShouldBe(ownerUser.Id);
        stored.InvitedEmail.ShouldBeNull();
        stored.InvitedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Return404_When_NotOwner()
    {
        using var owner = new AuthClient(factory);
        using var linked = new AuthClient(factory);
        var ownerUser = await RegisterAsync(owner);
        var linkedUser = await RegisterAsync(linked);
        var (group, members) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Trip");
        var (other, otherMembers) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Other");
        var (olya, max) = (members[1], members[2]);
        await TestData.LinkMemberAsync(factory, olya, linkedUser.Id);
        await TestData.InviteMemberAsync(factory, max, "max@example.com");
        await TestData.LinkMemberAsync(factory, otherMembers[1], linkedUser.Id);
        await TestData.InviteMemberAsync(factory, otherMembers[2], "other@example.com");
        var unknown = Guid.NewGuid();
        var invite = new { email = "eve@example.com" };

        var refused = new (AuthClient Client, HttpMethod Method, string Path, object? Body)[]
        {
            (linked, HttpMethod.Put, LinkPath(group.Id, olya.Id), null),
            (linked, HttpMethod.Put, LinkPath(group.Id, max.Id), null),
            (linked, HttpMethod.Delete, LinkPath(group.Id, olya.Id), null),
            (linked, HttpMethod.Put, InvitePath(group.Id, max.Id), invite),
            (linked, HttpMethod.Delete, InvitePath(group.Id, max.Id), null),
            (owner, HttpMethod.Put, LinkPath(group.Id, otherMembers[0].Id), null),
            (owner, HttpMethod.Delete, LinkPath(group.Id, otherMembers[1].Id), null),
            (owner, HttpMethod.Put, InvitePath(group.Id, otherMembers[0].Id), invite),
            (owner, HttpMethod.Delete, InvitePath(group.Id, otherMembers[2].Id), null),
            (owner, HttpMethod.Put, LinkPath(group.Id, unknown), null),
            (owner, HttpMethod.Put, InvitePath(group.Id, unknown), invite),
            (owner, HttpMethod.Put, LinkPath(unknown, members[0].Id), null),
            (owner, HttpMethod.Delete, LinkPath(unknown, olya.Id), null),
            (owner, HttpMethod.Put, InvitePath(unknown, members[0].Id), invite),
            (owner, HttpMethod.Delete, InvitePath(unknown, max.Id), null),
            (owner, HttpMethod.Put, $"{GroupPath(group.Id)}/members/not-a-guid/link", null)
        };

        foreach (var (client, method, path, body) in refused)
        {
            using var response = await SendAsync(client, method, path, body);

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound, $"{method} {path}");
        }

        using var invalidBody = await SendAsync(
            owner,
            HttpMethod.Put,
            InvitePath(unknown, members[0].Id),
            new { email = "no-at-sign" });

        await ShouldHaveErrorsAsync(invalidBody, ("email", "validation.invalidEmail"));
        var stored = await StoredMembersAsync(group.Id);
        stored.Select(member => member.UserId).ShouldBe([null, linkedUser.Id, null]);
        stored.Select(member => member.InvitedEmail).ShouldBe([null, null, "max@example.com"]);
        var otherStored = await StoredMembersAsync(other.Id);
        otherStored.Select(member => member.UserId).ShouldBe([null, linkedUser.Id, null]);
        otherStored.Select(member => member.InvitedEmail).ShouldBe([null, null, "other@example.com"]);
        (await GetJsonAsync(linked, GroupPath(group.Id))).GetProperty("myMemberId").GetGuid().ShouldBe(olya.Id);
    }

    [Fact]
    public async Task Should_RemoveLinkedMember_When_Unused()
    {
        using var owner = new AuthClient(factory);
        using var linked = new AuthClient(factory);
        var ownerUser = await RegisterAsync(owner);
        var linkedUser = await RegisterAsync(linked);
        var (group, members) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Trip");
        var (olya, max) = (members[1], members[2]);
        await TestData.InviteMemberAsync(factory, max, "max@example.com");
        await TestData.LinkMemberAsync(factory, olya, linkedUser.Id);

        using var removedMax = await SendAsync(owner, HttpMethod.Delete, MemberPath(group.Id, max.Id));
        using var removedOlya = await SendAsync(owner, HttpMethod.Delete, MemberPath(group.Id, olya.Id));

        removedMax.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        removedOlya.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await StoredMembersAsync(group.Id)).Select(member => member.Name).ShouldBe(["Anna"]);
        using var lostAccess = await linked.Http.GetAsync(GroupPath(group.Id), Ct);
        lostAccess.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_LinkOwnerOnce_When_RequestsRace()
    {
        using var owner = new AuthClient(factory);
        var ownerUser = await RegisterAsync(owner);
        var (group, members) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Trip");

        var responses = await Task.WhenAll(
            members.Select(member => SendAsync(owner, HttpMethod.Put, LinkPath(group.Id, member.Id))));

        try
        {
            responses.Count(response => response.StatusCode == HttpStatusCode.OK).ShouldBe(1);
            foreach (var refused in responses.Where(response => response.StatusCode != HttpStatusCode.OK))
            {
                await ShouldHaveErrorsAsync(refused, ("memberId", "validation.alreadyLinked"));
            }
        }
        finally
        {
            DisposeAll(responses);
        }

        (await StoredMembersAsync(group.Id)).Count(member => member.UserId == ownerUser.Id).ShouldBe(1);
    }

    [Fact]
    public async Task Should_InviteOnce_When_SameEmailSentConcurrently()
    {
        using var owner = new AuthClient(factory);
        var ownerUser = await RegisterAsync(owner);
        var (group, members) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Trip");

        var responses = await Task.WhenAll(
            members.Select(member =>
                SendAsync(owner, HttpMethod.Put, InvitePath(group.Id, member.Id), new { email = "same@example.com" })));

        try
        {
            responses.Count(response => response.StatusCode == HttpStatusCode.OK).ShouldBe(1);
            foreach (var refused in responses.Where(response => response.StatusCode != HttpStatusCode.OK))
            {
                await ShouldHaveErrorsAsync(refused, ("email", "validation.alreadyInvited"));
            }
        }
        finally
        {
            DisposeAll(responses);
        }

        (await StoredMembersAsync(group.Id)).Count(member => member.InvitedEmail == "same@example.com").ShouldBe(1);
    }

    private static string GroupPath(Guid groupId) => $"{GroupsPath}/{groupId}";

    private static string MemberPath(Guid groupId, Guid memberId) => $"{GroupPath(groupId)}/members/{memberId}";

    private static string LinkPath(Guid groupId, Guid memberId) => $"{MemberPath(groupId, memberId)}/link";

    private static string InvitePath(Guid groupId, Guid memberId) => $"{MemberPath(groupId, memberId)}/invite";

    private static List<JsonElement> Members(JsonElement group) => [.. group.GetProperty("members").EnumerateArray()];

    private static string? InvitedEmailOf(JsonElement member) => member.GetProperty("invitedEmail").GetString();

    private static async Task<RegisteredUser> RegisterAsync(AuthClient client)
    {
        var credentials = await client.RegisterNewAsync(Ct);
        var me = await GetJsonAsync(client, "/v1/me");
        return new RegisteredUser(me.GetProperty("id").GetGuid(), credentials.Name, credentials.Email);
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

    private static async Task ShouldHaveErrorsAsync(HttpResponseMessage response, params (string Field, string Code)[] expected)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await ReadAsync(response)).GetProperty("errors");
        errors.EnumerateObject()
            .Select(property => (property.Name, string.Join(",", property.Value.EnumerateArray().Select(code => code.GetString()))))
            .OrderBy(error => error.Name, StringComparer.Ordinal)
            .ShouldBe(expected.OrderBy(error => error.Field, StringComparer.Ordinal));
    }

    private static void DisposeAll(IEnumerable<HttpResponseMessage> responses)
    {
        foreach (var response in responses)
        {
            response.Dispose();
        }
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

    private sealed record RegisteredUser(Guid Id, string Name, string Email);
}
