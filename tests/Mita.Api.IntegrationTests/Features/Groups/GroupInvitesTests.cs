using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.Common.Groups;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Groups;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Groups;

public sealed class GroupInvitesTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string GroupsPath = "/v1/groups";
    private const string InvitesPath = "/v1/group-invites";
    private const string MemberLinked = "validation.memberLinked";
    private const string AlreadyLinked = "validation.alreadyLinked";

    private static readonly string[] InviteProperties =
        ["memberId", "groupId", "groupName", "memberName", "ownerName", "invitedAt"];

    private static readonly string[] SummaryProperties = ["id", "name", "currency", "members", "total", "role"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_ListInvites_ForMatchingEmail()
    {
        using var owner = new AuthClient(factory);
        using var invitee = new AuthClient(factory);
        using var other = new AuthClient(factory);
        var ownerUser = await RegisterAsync(owner);
        var inviteeUser = await RegisterAsync(
            invitee,
            new TestCredentials("Bohdan", $"B-{Guid.NewGuid():N}@Example.COM", $"pw-{Guid.NewGuid():N}"));
        await RegisterAsync(other);
        var (group, members) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Trip");
        var (hike, hikeMembers) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Hike");
        var (olya, max) = (members[1], members[2]);
        var before = DateTimeOffset.UtcNow;
        await InviteAsync(owner, group.Id, olya.Id, inviteeUser.Email.ToUpperInvariant());
        await InviteAsync(owner, hike.Id, hikeMembers[0].Id, inviteeUser.Email);

        using var response = await invitee.Http.GetAsync($"{InvitesPath}?page=1&pageSize=100", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await ReadAsync(response);
        page.GetProperty("page").GetInt32().ShouldBe(1);
        page.GetProperty("pageSize").GetInt32().ShouldBe(100);
        page.GetProperty("total").GetInt32().ShouldBe(2);
        var invites = page.GetProperty("items").EnumerateArray().ToList();
        invites.Select(invite => invite.GetProperty("memberId").GetGuid()).ShouldBe([hikeMembers[0].Id, olya.Id]);
        var trip = invites[1];
        trip.EnumerateObject().Select(property => property.Name).ShouldBe(InviteProperties);
        trip.GetProperty("groupId").GetGuid().ShouldBe(group.Id);
        trip.GetProperty("groupName").GetString().ShouldBe("Trip");
        trip.GetProperty("memberName").GetString().ShouldBe("Olya");
        trip.GetProperty("ownerName").GetString().ShouldBe(ownerUser.Name);
        trip.GetProperty("invitedAt").GetString()!.ShouldMatch(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$");
        var invitedAt = trip.GetProperty("invitedAt").GetDateTimeOffset();
        invitedAt.ShouldBeGreaterThanOrEqualTo(before.AddSeconds(-5));
        invitedAt.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow.AddSeconds(5));
        invites[0].GetProperty("groupName").GetString().ShouldBe("Hike");
        invites[0].GetProperty("memberName").GetString().ShouldBe("Anna");
        (await ListInvitesAsync(other)).ShouldBeEmpty();
        (await ListInvitesAsync(owner)).ShouldBeEmpty();

        await TestData.LinkMemberAsync(factory, max, inviteeUser.Id);

        (await ListInvitesAsync(invitee)).Select(invite => invite.GetProperty("memberId").GetGuid())
            .ShouldBe([hikeMembers[0].Id]);
    }

    [Fact]
    public async Task Should_AcceptInvite_OnlyForMatchingEmail()
    {
        using var owner = new AuthClient(factory);
        using var invitee = new AuthClient(factory);
        using var other = new AuthClient(factory);
        var ownerUser = await RegisterAsync(owner);
        var inviteeUser = await RegisterAsync(invitee);
        var otherUser = await RegisterAsync(other);
        var (group, members) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Trip");
        var (hike, hikeMembers) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Hike");
        var olya = members[1];
        await InviteAsync(owner, group.Id, olya.Id, inviteeUser.Email);

        using var stranger = await SendAsync(other, HttpMethod.Post, AcceptPath(olya.Id));

        await ShouldBeNotFoundAsync(stranger);
        (await StoredMembersAsync(group.Id))[1].UserId.ShouldBeNull();

        using var accepted = await SendAsync(invitee, HttpMethod.Post, AcceptPath(olya.Id));

        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        var summary = await ReadAsync(accepted);
        summary.EnumerateObject().Select(property => property.Name).ShouldBe(SummaryProperties);
        summary.GetProperty("id").GetGuid().ShouldBe(group.Id);
        summary.GetProperty("name").GetString().ShouldBe("Trip");
        summary.GetProperty("currency").GetString().ShouldBe("EUR");
        summary.GetProperty("members").GetInt32().ShouldBe(3);
        summary.GetProperty("total").GetInt64().ShouldBe(0);
        summary.GetProperty("role").GetString().ShouldBe("member");
        var stored = (await StoredMembersAsync(group.Id))[1];
        stored.UserId.ShouldBe(inviteeUser.Id);
        stored.InvitedEmail.ShouldBeNull();
        stored.InvitedAt.ShouldBeNull();
        var ownerView = Members(await GetJsonAsync(owner, GroupPath(group.Id)))[1];
        ownerView.GetProperty("linked").GetBoolean().ShouldBeTrue();
        ownerView.GetProperty("linkedName").GetString().ShouldBe(inviteeUser.Name);
        ownerView.GetProperty("invitedEmail").ValueKind.ShouldBe(JsonValueKind.Null);
        var listed = (await GetJsonAsync(invitee, GroupsPath)).GetProperty("items").EnumerateArray().ToList();
        listed.ShouldHaveSingleItem().GetRawText().ShouldBe(summary.GetRawText());
        (await GetJsonAsync(invitee, GroupPath(group.Id))).GetProperty("myMemberId").GetGuid().ShouldBe(olya.Id);

        using var again = await SendAsync(invitee, HttpMethod.Post, AcceptPath(olya.Id));

        await ShouldBeNotFoundAsync(again);

        var (cancelledMember, linkedMember) = (hikeMembers[1], hikeMembers[2]);
        await InviteAsync(owner, hike.Id, cancelledMember.Id, inviteeUser.Email);
        using (var cancel = await SendAsync(owner, HttpMethod.Delete, $"{MemberPath(hike.Id, cancelledMember.Id)}/invite"))
        {
            cancel.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        await TestData.InviteMemberAsync(factory, linkedMember, inviteeUser.Email);
        await TestData.LinkMemberAsync(factory, linkedMember, otherUser.Id);

        using var cancelled = await SendAsync(invitee, HttpMethod.Post, AcceptPath(cancelledMember.Id));
        using var linkedElsewhere = await SendAsync(invitee, HttpMethod.Post, AcceptPath(linkedMember.Id));
        using var unknown = await SendAsync(invitee, HttpMethod.Post, AcceptPath(Guid.NewGuid()));
        using var malformed = await SendAsync(invitee, HttpMethod.Post, $"{InvitesPath}/not-a-guid/accept");

        await ShouldBeNotFoundAsync(cancelled);
        await ShouldBeNotFoundAsync(linkedElsewhere);
        await ShouldBeNotFoundAsync(unknown);
        malformed.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await StoredMembersAsync(hike.Id)).Select(member => member.UserId).ShouldBe([null, null, otherUser.Id]);
        using var lostAccess = await invitee.Http.GetAsync(GroupPath(hike.Id), Ct);
        lostAccess.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_DeclineInvite()
    {
        using var owner = new AuthClient(factory);
        using var invitee = new AuthClient(factory);
        using var other = new AuthClient(factory);
        var ownerUser = await RegisterAsync(owner);
        var inviteeUser = await RegisterAsync(invitee);
        await RegisterAsync(other);
        var (group, members) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Trip");
        var olya = members[1];
        await InviteAsync(owner, group.Id, olya.Id, inviteeUser.Email);

        using var stranger = await SendAsync(other, HttpMethod.Post, DeclinePath(olya.Id));

        await ShouldBeNotFoundAsync(stranger);
        (await StoredMembersAsync(group.Id))[1].InvitedEmail.ShouldBe(inviteeUser.Email);

        using var declined = await SendAsync(invitee, HttpMethod.Post, DeclinePath(olya.Id));

        declined.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await declined.Content.ReadAsStringAsync(Ct)).ShouldBeEmpty();
        Members(await GetJsonAsync(owner, GroupPath(group.Id)))
            .Select(member => member.GetProperty("invitedEmail").GetString())
            .ShouldAllBe(email => email == null);
        var stored = (await StoredMembersAsync(group.Id))[1];
        stored.UserId.ShouldBeNull();
        stored.InvitedEmail.ShouldBeNull();
        stored.InvitedAt.ShouldBeNull();
        (await ListInvitesAsync(invitee)).ShouldBeEmpty();
        using var noAccess = await invitee.Http.GetAsync(GroupPath(group.Id), Ct);
        noAccess.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var again = await SendAsync(invitee, HttpMethod.Post, DeclinePath(olya.Id));
        using var unknown = await SendAsync(invitee, HttpMethod.Post, DeclinePath(Guid.NewGuid()));

        await ShouldBeNotFoundAsync(again);
        await ShouldBeNotFoundAsync(unknown);
    }

    [Fact]
    public async Task Should_RefuseAccept_When_AlreadyLinkedInGroup()
    {
        using var owner = new AuthClient(factory);
        using var invitee = new AuthClient(factory);
        var ownerUser = await RegisterAsync(owner);
        var inviteeUser = await RegisterAsync(invitee);
        var (group, members) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Trip");
        var (olya, max) = (members[1], members[2]);
        await TestData.LinkMemberAsync(factory, max, inviteeUser.Id);
        await TestData.InviteMemberAsync(factory, olya, inviteeUser.Email);

        using var refused = await SendAsync(invitee, HttpMethod.Post, AcceptPath(olya.Id));

        await ShouldHaveErrorsAsync(refused, ("memberId", AlreadyLinked));
        var stored = await StoredMembersAsync(group.Id);
        stored.Select(member => member.UserId).ShouldBe([null, null, inviteeUser.Id]);
        stored.Select(member => member.InvitedEmail).ShouldBe([null, inviteeUser.Email, null]);
    }

    [Fact]
    public async Task Should_FollowEmailChange()
    {
        using var owner = new AuthClient(factory);
        using var invitee = new AuthClient(factory);
        var ownerUser = await RegisterAsync(owner);
        var inviteeUser = await RegisterAsync(
            invitee,
            new TestCredentials("Bohdan", "old@example.com", $"pw-{Guid.NewGuid():N}"));
        var (group, members) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Trip");
        var olya = members[1];
        await InviteAsync(owner, group.Id, olya.Id, "old@example.com");
        (await ListInvitesAsync(invitee)).ShouldHaveSingleItem();

        using (var changed = await SendAsync(
                   invitee,
                   HttpMethod.Put,
                   "/v1/me",
                   new { name = inviteeUser.Name, email = $"new-{Guid.NewGuid():N}@example.com" }))
        {
            changed.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        (await ListInvitesAsync(invitee)).ShouldBeEmpty();

        using var accepted = await SendAsync(invitee, HttpMethod.Post, AcceptPath(olya.Id));
        using var declined = await SendAsync(invitee, HttpMethod.Post, DeclinePath(olya.Id));

        await ShouldBeNotFoundAsync(accepted);
        await ShouldBeNotFoundAsync(declined);
        var stored = (await StoredMembersAsync(group.Id))[1];
        stored.UserId.ShouldBeNull();
        stored.InvitedEmail.ShouldBe("old@example.com");
    }

    [Fact]
    public async Task Should_HideInvite_When_AccountEmailMatchesOnlyAfterLowerCasing()
    {
        using var owner = new AuthClient(factory);
        using var invitee = new AuthClient(factory);
        using var lookalike = new AuthClient(factory);
        var ownerUser = await RegisterAsync(owner);
        var local = $"max-{Guid.NewGuid():N}";
        var sharpS = $"{local}@stra\u00DFe.de";
        var inviteeUser = await RegisterAsync(invitee, new TestCredentials("Max", sharpS, $"pw-{Guid.NewGuid():N}"));
        await RegisterAsync(lookalike, new TestCredentials("Maks", $"{local}@stra\u1E9Ee.de", $"pw-{Guid.NewGuid():N}"));
        var (group, members) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Trip");
        var olya = members[1];
        await InviteAsync(owner, group.Id, olya.Id, sharpS);

        (await ListInvitesAsync(lookalike)).ShouldBeEmpty();
        using var lookalikeAccepted = await SendAsync(lookalike, HttpMethod.Post, AcceptPath(olya.Id));
        using var lookalikeDeclined = await SendAsync(lookalike, HttpMethod.Post, DeclinePath(olya.Id));

        await ShouldBeNotFoundAsync(lookalikeAccepted);
        await ShouldBeNotFoundAsync(lookalikeDeclined);
        var untouched = (await StoredMembersAsync(group.Id))[1];
        untouched.UserId.ShouldBeNull();
        untouched.InvitedEmail.ShouldBe(sharpS);
        (await ListInvitesAsync(invitee)).Select(invite => invite.GetProperty("memberId").GetGuid()).ShouldBe([olya.Id]);

        using var accepted = await SendAsync(invitee, HttpMethod.Post, AcceptPath(olya.Id));

        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StoredMembersAsync(group.Id))[1].UserId.ShouldBe(inviteeUser.Id);
    }

    [Fact]
    public async Task Should_MatchInvite_When_EmailIsTypedDecomposed()
    {
        using var owner = new AuthClient(factory);
        using var invitee = new AuthClient(factory);
        var ownerUser = await RegisterAsync(owner);
        var local = $"rene-{Guid.NewGuid():N}";
        var composed = $"{local}@caf\u00E9.fr";
        var decomposed = $"{local}@cafe\u0301.fr";
        var inviteeUser = await RegisterAsync(invitee, new TestCredentials("Ren\u00E9", composed, $"pw-{Guid.NewGuid():N}"));
        var (group, members) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Trip");
        var (inviteeGroup, inviteeMembers) = await TestData.AddGroupAsync(factory, inviteeUser.Id, "Hike");
        var olya = members[1];

        using var self = await SendAsync(
            invitee,
            HttpMethod.Put,
            $"{MemberPath(inviteeGroup.Id, inviteeMembers[0].Id)}/invite",
            new { email = decomposed });

        await ShouldHaveErrorsAsync(self, ("email", "validation.inviteSelf"));

        await InviteAsync(owner, group.Id, olya.Id, decomposed);

        (await StoredMembersAsync(group.Id))[1].InvitedEmail.ShouldBe(composed);
        (await ListInvitesAsync(invitee)).Select(invite => invite.GetProperty("memberId").GetGuid()).ShouldBe([olya.Id]);

        using var accepted = await SendAsync(invitee, HttpMethod.Post, AcceptPath(olya.Id));

        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StoredMembersAsync(group.Id))[1].UserId.ShouldBe(inviteeUser.Id);
    }

    [Fact]
    public async Task Should_LeaveGroup()
    {
        using var owner = new AuthClient(factory);
        using var linked = new AuthClient(factory);
        using var stranger = new AuthClient(factory);
        var ownerUser = await RegisterAsync(owner);
        var linkedUser = await RegisterAsync(linked);
        await RegisterAsync(stranger);
        var (group, members) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Trip");
        var (anna, olya) = (members[0], members[1]);
        await TestData.LinkMemberAsync(factory, olya, linkedUser.Id);

        using var strangerLeft = await SendAsync(stranger, HttpMethod.Delete, MembershipPath(group.Id));
        using var ownerNotLinked = await SendAsync(owner, HttpMethod.Delete, MembershipPath(group.Id));

        await ShouldBeNotFoundAsync(strangerLeft);
        await ShouldBeNotFoundAsync(ownerNotLinked);
        (await StoredMembersAsync(group.Id)).Select(member => member.UserId).ShouldBe([null, linkedUser.Id, null]);

        using var left = await SendAsync(linked, HttpMethod.Delete, MembershipPath(group.Id));

        left.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await left.Content.ReadAsStringAsync(Ct)).ShouldBeEmpty();
        using var lostAccess = await linked.Http.GetAsync(GroupPath(group.Id), Ct);
        lostAccess.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await StoredMembersAsync(group.Id)).Select(member => member.UserId).ShouldAllBe(userId => userId == null);
        (await GetJsonAsync(linked, GroupsPath)).GetProperty("items").GetArrayLength().ShouldBe(0);

        using var again = await SendAsync(linked, HttpMethod.Delete, MembershipPath(group.Id));
        using var unknown = await SendAsync(owner, HttpMethod.Delete, MembershipPath(Guid.NewGuid()));

        await ShouldBeNotFoundAsync(again);
        await ShouldBeNotFoundAsync(unknown);

        await TestData.LinkMemberAsync(factory, anna, ownerUser.Id);

        using var ownerLeft = await SendAsync(owner, HttpMethod.Delete, MembershipPath(group.Id));

        ownerLeft.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var read = await GetJsonAsync(owner, GroupPath(group.Id));
        read.GetProperty("role").GetString().ShouldBe("owner");
        read.GetProperty("myMemberId").ValueKind.ShouldBe(JsonValueKind.Null);
        (await StoredMembersAsync(group.Id)).Select(member => member.UserId).ShouldAllBe(userId => userId == null);
    }

    [Fact]
    public async Task Should_LinkThroughShareLink()
    {
        using var owner = new AuthClient(factory);
        using var visitor = new AuthClient(factory);
        using var other = new AuthClient(factory);
        using var link = new LinkClient(factory);
        var ownerUser = await RegisterAsync(owner);
        var visitorUser = await RegisterAsync(visitor);
        await RegisterAsync(other);
        var (group, members) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Trip");
        var (otherGroup, otherMembers) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Other");
        var (olya, max) = (members[1], members[2]);
        var token = await ShareAsync(owner, group.Id);
        await TestData.InviteMemberAsync(factory, olya, "olya@example.com");

        using var linked = await LinkThroughShareAsync(visitor, token, olya.Id.ToString());

        linked.StatusCode.ShouldBe(HttpStatusCode.OK);
        var summary = await ReadAsync(linked);
        summary.EnumerateObject().Select(property => property.Name).ShouldBe(SummaryProperties);
        summary.GetProperty("id").GetGuid().ShouldBe(group.Id);
        summary.GetProperty("name").GetString().ShouldBe("Trip");
        summary.GetProperty("role").GetString().ShouldBe("member");
        var stored = (await StoredMembersAsync(group.Id))[1];
        stored.UserId.ShouldBe(visitorUser.Id);
        stored.InvitedEmail.ShouldBeNull();
        stored.InvitedAt.ShouldBeNull();
        var read = await GetJsonAsync(visitor, GroupPath(group.Id));
        read.GetProperty("role").GetString().ShouldBe("member");
        read.GetProperty("myMemberId").GetGuid().ShouldBe(olya.Id);

        using var again = await LinkThroughShareAsync(visitor, token, olya.Id.ToString());

        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync(again)).GetRawText().ShouldBe(summary.GetRawText());

        using var second = await LinkThroughShareAsync(visitor, token, max.Id.ToString());
        using var taken = await LinkThroughShareAsync(other, token, olya.Id.ToString());

        await ShouldHaveErrorsAsync(second, ("memberId", AlreadyLinked));
        await ShouldHaveErrorsAsync(taken, ("memberId", MemberLinked));

        using var anonymous = await link.SendAsync(HttpMethod.Put, token, "/membership", member: max.Id.ToString());
        using var anonymousUnknown = await link.SendAsync(
            HttpMethod.Put,
            ShareTokens.New(),
            "/membership",
            member: max.Id.ToString());
        using var unknown = await LinkThroughShareAsync(other, ShareTokens.New(), max.Id.ToString());
        using var unknownWithoutXsrf = await LinkThroughShareAsync(other, ShareTokens.New(), max.Id.ToString(), xsrf: false);
        using var noHeader = await LinkThroughShareAsync(other, token, null);
        using var foreignMember = await LinkThroughShareAsync(other, token, otherMembers[2].Id.ToString());
        using var malformedMember = await LinkThroughShareAsync(other, token, "not-a-guid");
        using var withoutXsrf = await LinkThroughShareAsync(other, token, max.Id.ToString(), xsrf: false);
        using var withoutXsrfOrHeader = await LinkThroughShareAsync(other, token, null, xsrf: false);

        anonymous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await ShouldBeNotFoundAsync(anonymousUnknown);
        await ShouldBeNotFoundAsync(unknown);
        await ShouldBeNotFoundAsync(unknownWithoutXsrf);
        await ShouldHaveCodeAsync(noHeader, HttpStatusCode.Forbidden, "group.memberRequired");
        await ShouldHaveCodeAsync(foreignMember, HttpStatusCode.Forbidden, "group.memberRequired");
        await ShouldHaveCodeAsync(malformedMember, HttpStatusCode.Forbidden, "group.memberRequired");
        await ShouldHaveCodeAsync(withoutXsrf, HttpStatusCode.Forbidden, "auth.xsrf");
        await ShouldHaveCodeAsync(withoutXsrfOrHeader, HttpStatusCode.Forbidden, "auth.xsrf");
        (await StoredMembersAsync(group.Id)).Select(member => member.UserId).ShouldBe([null, visitorUser.Id, null]);
        (await StoredMembersAsync(otherGroup.Id)).Select(member => member.UserId).ShouldAllBe(userId => userId == null);
        Members(await GetJsonAsync(owner, GroupPath(group.Id)))[1].GetProperty("linkedName").GetString()
            .ShouldBe(visitorUser.Name);
    }

    [Fact]
    public async Task Should_LinkOnce_When_RequestsRace()
    {
        using var owner = new AuthClient(factory);
        using var visitor = new AuthClient(factory);
        var ownerUser = await RegisterAsync(owner);
        var visitorUser = await RegisterAsync(visitor);

        // A first round runs cold code paths that seldom overlap; the later rounds are the ones that race.
        for (var round = 0; round < 3; round++)
        {
            var (group, members) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Trip");
            var (hike, hikeMembers) = await TestData.AddGroupAsync(factory, ownerUser.Id, "Hike");
            var token = await ShareAsync(owner, group.Id);
            var hikeToken = await ShareAsync(owner, hike.Id);
            await TestData.InviteMemberAsync(factory, hikeMembers[1], visitorUser.Email);

            await ShouldLinkOnceAsync(
                group.Id,
                visitorUser.Id,
                LinkThroughShareAsync(visitor, token, members[1].Id.ToString()),
                LinkThroughShareAsync(visitor, token, members[2].Id.ToString()));
            await ShouldLinkOnceAsync(
                hike.Id,
                visitorUser.Id,
                LinkThroughShareAsync(visitor, hikeToken, hikeMembers[2].Id.ToString()),
                SendAsync(visitor, HttpMethod.Post, AcceptPath(hikeMembers[1].Id)));
        }
    }

    private static string GroupPath(Guid groupId) => $"{GroupsPath}/{groupId}";

    private static string MemberPath(Guid groupId, Guid memberId) => $"{GroupPath(groupId)}/members/{memberId}";

    private static string MembershipPath(Guid groupId) => $"{GroupPath(groupId)}/membership";

    private static string AcceptPath(Guid memberId) => $"{InvitesPath}/{memberId}/accept";

    private static string DeclinePath(Guid memberId) => $"{InvitesPath}/{memberId}/decline";

    private static List<JsonElement> Members(JsonElement group) => [.. group.GetProperty("members").EnumerateArray()];

    private static async Task<RegisteredUser> RegisterAsync(AuthClient client, TestCredentials? credentials = null)
    {
        if (credentials is null)
        {
            credentials = await client.RegisterNewAsync(Ct);
        }
        else
        {
            using var registered = await client.RegisterAsync(credentials, ct: Ct);
            registered.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        var me = await GetJsonAsync(client, "/v1/me");
        return new RegisteredUser(
            me.GetProperty("id").GetGuid(),
            me.GetProperty("name").GetString()!,
            GroupInviteEmails.Normalize(me.GetProperty("email").GetString()!));
    }

    private static async Task<JsonElement> GetJsonAsync(AuthClient client, string path)
    {
        using var response = await client.Http.GetAsync(path, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, path);
        return await ReadAsync(response);
    }

    private static async Task<List<JsonElement>> ListInvitesAsync(AuthClient client) =>
        [.. (await GetJsonAsync(client, InvitesPath)).GetProperty("items").EnumerateArray()];

    private static async Task InviteAsync(AuthClient owner, Guid groupId, Guid memberId, string email)
    {
        using var response = await SendAsync(owner, HttpMethod.Put, $"{MemberPath(groupId, memberId)}/invite", new { email });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<string> ShareAsync(AuthClient owner, Guid groupId)
    {
        using var response = await SendAsync(owner, HttpMethod.Post, $"{GroupPath(groupId)}/share");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await ReadAsync(response)).GetProperty("shareToken").GetString()!;
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

    private static async Task<HttpResponseMessage> LinkThroughShareAsync(
        AuthClient client,
        string token,
        string? member,
        bool xsrf = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, LinkClient.PathOf(token, "/membership"));
        if (member is not null)
        {
            request.Headers.TryAddWithoutValidation(LinkClient.MemberHeader, member);
        }

        return await client.Http.SendAsync(xsrf ? client.WithXsrf(request) : request, Ct);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    private static async Task ShouldBeNotFoundAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        (await ReadAsync(response)).GetProperty("title").GetString().ShouldBe("Not Found");
    }

    private static async Task ShouldHaveCodeAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.ShouldBe(status, code);
        (await ReadAsync(response)).GetProperty("code").GetString().ShouldBe(code);
    }

    private static async Task ShouldHaveErrorsAsync(HttpResponseMessage response, params (string Field, string Code)[] expected)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadAsync(response)).GetProperty("errors").EnumerateObject()
            .Select(error => (error.Name, error.Value.EnumerateArray().Single().GetString()!))
            .ShouldBe(expected, ignoreOrder: true);
    }

    private async Task ShouldLinkOnceAsync(Guid groupId, Guid userId, params Task<HttpResponseMessage>[] racing)
    {
        var responses = await Task.WhenAll(racing);

        try
        {
            responses.Count(response => response.StatusCode == HttpStatusCode.OK).ShouldBe(1);
            await ShouldHaveErrorsAsync(
                responses.Single(response => response.StatusCode != HttpStatusCode.OK),
                ("memberId", AlreadyLinked));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        (await StoredMembersAsync(groupId)).Count(member => member.UserId == userId).ShouldBe(1);
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
