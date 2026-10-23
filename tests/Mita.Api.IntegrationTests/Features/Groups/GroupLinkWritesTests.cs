using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Features.Groups.CreateExpense;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Groups;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Groups;

public sealed class GroupLinkWritesTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string GroupsPath = "/v1/groups";

    private static readonly StoredCounts Empty = new(0, 0, 0, 0);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Should_RequireMember_ForLinkWrites()
    {
        using var owner = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        var ownerId = await UserIdAsync(owner);
        var (group, members) = await TestData.AddGroupAsync(factory, ownerId, "Trip");
        var (_, otherMembers) = await TestData.AddGroupAsync(factory, ownerId, "Other");
        var token = await ShareAsync(owner, group.Id);
        using var link = new LinkClient(factory);
        var expense = ExpenseBody(members[0].Id);

        string?[] refused = [null, "x", Guid.NewGuid().ToString(), otherMembers[0].Id.ToString()];
        foreach (var member in refused)
        {
            using var response = await link.SendAsync(HttpMethod.Post, token, "/expenses", expense, member);

            await ShouldBeMemberRequiredAsync(response, member ?? "no header");
        }

        (await StoredCountsAsync(group.Id)).ShouldBe(Empty);

        using var invalid = await link.SendAsync(
            HttpMethod.Post,
            token,
            "/expenses",
            new { payerId = members[0].Id, amount = 0, currency = "EUR", date = IsoDay(Today) });

        await ShouldHaveErrorsAsync(invalid, ("amount", "validation.positiveAmount"));

        using var created = await link.SendAsync(HttpMethod.Post, token, "/expenses", expense, members[1].Id.ToString());

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await StoredCountsAsync(group.Id)).ShouldBe(new StoredCounts(1, 0, 1, 0));
    }

    [Fact]
    public async Task Should_RequireMember_OnEveryLinkWrite()
    {
        using var owner = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(owner), "Trip");
        var (anna, max) = (members[0].Id, members[2].Id);
        var liveExpense = await TestData.AddGroupExpenseAsync(factory, group, members[0], 9_000, members);
        var deletedExpense = await TestData.AddGroupExpenseAsync(factory, group, members[1], 3_000, members, deleted: true);
        var liveRepayment = await TestData.AddGroupRepaymentAsync(factory, group, members[2], members[0], 2_000);
        var deletedRepayment = await TestData.AddGroupRepaymentAsync(
            factory, group, members[1], members[0], 1_000, deleted: true);
        var token = await ShareAsync(owner, group.Id);
        using var link = new LinkClient(factory);
        var before = await StoredCountsAsync(group.Id);
        var writes = new (HttpMethod Method, string Path, object? Body)[]
        {
            (HttpMethod.Post, "/categories", new { name = "Ferry" }),
            (HttpMethod.Post, "/expenses", ExpenseBody(anna)),
            (HttpMethod.Put, $"/expenses/{liveExpense.Id}", ExpenseBody(anna, 12_000)),
            (HttpMethod.Delete, $"/expenses/{liveExpense.Id}", null),
            (HttpMethod.Post, $"/expenses/{deletedExpense.Id}/restore", null),
            (HttpMethod.Post, "/repayments", RepaymentBody(max, anna)),
            (HttpMethod.Put, $"/repayments/{liveRepayment.Id}", RepaymentBody(max, anna, 2_500)),
            (HttpMethod.Delete, $"/repayments/{liveRepayment.Id}", null),
            (HttpMethod.Post, $"/repayments/{deletedRepayment.Id}/restore", null)
        };

        foreach (var (method, path, body) in writes)
        {
            using var response = await link.SendAsync(method, token, path, body);

            await ShouldBeMemberRequiredAsync(response, $"{method} {path}");
        }

        (await StoredCountsAsync(group.Id)).ShouldBe(before);
        (await DeletedEntriesAsync(group.Id)).ShouldBe([deletedExpense.Id, deletedRepayment.Id], ignoreOrder: true);

        var unknownEntry = Guid.NewGuid();
        using var unknownWithoutMember = await link.SendAsync(HttpMethod.Delete, token, $"/expenses/{unknownEntry}");
        using var unknownWithMember = await link.SendAsync(
            HttpMethod.Delete,
            token,
            $"/expenses/{unknownEntry}",
            member: anna.ToString());

        await ShouldBeMemberRequiredAsync(unknownWithoutMember, "unknown entry without a member");
        await ShouldBeNotFoundAsync(unknownWithMember);
    }

    [Fact]
    public async Task Should_RecordMemberAsActor_When_LinkHolderWrites()
    {
        using var owner = new AuthClient(factory);
        var credentials = await owner.RegisterNewAsync(Ct);
        var ownerId = await UserIdAsync(owner);
        var (group, members) = await TestData.AddGroupAsync(factory, ownerId, "Trip");
        var (anna, olya, max) = (members[0].Id, members[1].Id, members[2].Id);
        var token = await ShareAsync(owner, group.Id);
        using var link = new LinkClient(factory);
        var expense = new
        {
            payerId = anna,
            amount = 9_000,
            currency = "EUR",
            date = IsoDay(Today),
            split = new { mode = "equal", memberIds = new[] { anna, max } }
        };
        (await InUseAsync(link, token)).ShouldBe([("Anna", false), ("Olya", false), ("Max", false)]);

        using var created = await link.SendAsync(HttpMethod.Post, token, "/expenses", expense, olya.ToString());

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdBody = await ReadAsync(created);
        var expenseId = createdBody.GetProperty("id").GetGuid();
        ActorOf(createdBody.GetProperty("createdBy")).ShouldBe(Member(olya, "Olya"));
        createdBody.GetProperty("updatedBy").ValueKind.ShouldBe(JsonValueKind.Null);
        createdBody.GetProperty("shares").EnumerateArray()
            .Select(share => share.GetProperty("memberId").GetGuid())
            .ShouldBe([anna, max]);
        (await InUseAsync(link, token)).ShouldBe([("Anna", true), ("Olya", true), ("Max", true)]);

        using var edited = await OwnerSendAsync(
            owner,
            HttpMethod.Put,
            $"{GroupsPath}/{group.Id}/expenses/{expenseId}",
            expense with { amount = 12_000 });

        edited.StatusCode.ShouldBe(HttpStatusCode.OK);
        var editedBody = await ReadAsync(edited);
        ActorOf(editedBody.GetProperty("createdBy")).ShouldBe(Member(olya, "Olya"));
        ActorOf(editedBody.GetProperty("updatedBy")).ShouldBe(Owner(credentials.Name));

        using var deleted = await link.SendAsync(HttpMethod.Delete, token, $"/expenses/{expenseId}", member: olya.ToString());
        using var restored = await link.SendAsync(
            HttpMethod.Post,
            token,
            $"/expenses/{expenseId}/restore",
            member: olya.ToString());

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        restored.StatusCode.ShouldBe(HttpStatusCode.OK);
        var restoredBody = await ReadAsync(restored);
        restoredBody.GetProperty("deleted").GetBoolean().ShouldBeFalse();
        restoredBody.GetProperty("amount").GetInt64().ShouldBe(12_000);
        ActorOf(restoredBody.GetProperty("createdBy")).ShouldBe(Member(olya, "Olya"));
        ActorOf(restoredBody.GetProperty("updatedBy")).ShouldBe(Member(olya, "Olya"));

        using var history = await link.GetAsync(token, $"/expenses/{expenseId}/history");

        history.StatusCode.ShouldBe(HttpStatusCode.OK);
        var events = (await ReadAsync(history)).GetProperty("items").EnumerateArray().ToList();
        events.Select(entry => entry.GetProperty("action").GetString())
            .ShouldBe(["restored", "deleted", "updated", "created"]);
        events.Select(entry => ActorOf(entry.GetProperty("actor")))
            .ShouldBe([Member(olya, "Olya"), Member(olya, "Olya"), Owner(credentials.Name), Member(olya, "Olya")]);
        (Guid? UserId, Guid? MemberId)[] stored = [(null, olya), (null, olya), (ownerId, null), (null, olya)];
        (await StoredActorsAsync(expenseId)).ShouldBe(stored);
    }

    [Fact]
    public async Task Should_WriteRepaymentsAndCategories_ThroughLink()
    {
        using var owner = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(owner), "Trip");
        var (anna, olya, max) = (members[0].Id, members[1].Id, members[2].Id);
        var token = await ShareAsync(owner, group.Id);
        using var link = new LinkClient(factory);
        var asMax = max.ToString();

        using var created = await link.SendAsync(HttpMethod.Post, token, "/repayments", RepaymentBody(max, anna), asMax);

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdBody = await ReadAsync(created);
        var repaymentId = createdBody.GetProperty("id").GetGuid();
        createdBody.GetProperty("fromMemberId").GetGuid().ShouldBe(max);
        createdBody.GetProperty("toMemberId").GetGuid().ShouldBe(anna);
        ActorOf(createdBody.GetProperty("createdBy")).ShouldBe(Member(max, "Max"));

        using var edited = await link.SendAsync(
            HttpMethod.Put,
            token,
            $"/repayments/{repaymentId}",
            RepaymentBody(max, anna, 2_500),
            asMax);

        edited.StatusCode.ShouldBe(HttpStatusCode.OK);
        var editedBody = await ReadAsync(edited);
        editedBody.GetProperty("amount").GetInt64().ShouldBe(2_500);
        ActorOf(editedBody.GetProperty("updatedBy")).ShouldBe(Member(max, "Max"));

        using var deleted = await link.SendAsync(HttpMethod.Delete, token, $"/repayments/{repaymentId}", member: asMax);
        using var restored = await link.SendAsync(
            HttpMethod.Post,
            token,
            $"/repayments/{repaymentId}/restore",
            member: asMax);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        restored.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync(restored)).GetProperty("deleted").GetBoolean().ShouldBeFalse();
        using var history = await link.GetAsync(token, $"/repayments/{repaymentId}/history");
        var events = (await ReadAsync(history)).GetProperty("items").EnumerateArray().ToList();
        events.Select(entry => entry.GetProperty("action").GetString())
            .ShouldBe(["restored", "deleted", "updated", "created"]);
        events.Select(entry => ActorOf(entry.GetProperty("actor"))).ShouldBe(Enumerable.Repeat(Member(max, "Max"), 4));

        using var category = await link.SendAsync(HttpMethod.Post, token, "/categories", new { name = "Ferry" }, olya.ToString());

        category.StatusCode.ShouldBe(HttpStatusCode.Created);
        var categoryBody = await ReadAsync(category);
        categoryBody.GetProperty("standard").GetBoolean().ShouldBeFalse();
        categoryBody.GetProperty("name").GetString().ShouldBe("Ferry");
        using var linkGroup = await link.GetAsync(token);
        (await ReadAsync(linkGroup)).GetProperty("categories").EnumerateArray().Last().GetProperty("id").GetString()
            .ShouldBe(categoryBody.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Should_RefuseOtherGroupsEntries_OnLink()
    {
        using var owner = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        var ownerId = await UserIdAsync(owner);
        var (group, members) = await TestData.AddGroupAsync(factory, ownerId, "Shared");
        var (other, otherMembers) = await TestData.AddGroupAsync(factory, ownerId, "Other");
        var otherExpense = await TestData.AddGroupExpenseAsync(factory, other, otherMembers[0], 9_000, otherMembers);
        var otherDeletedExpense = await TestData.AddGroupExpenseAsync(
            factory, other, otherMembers[1], 3_000, otherMembers, deleted: true);
        var otherRepayment = await TestData.AddGroupRepaymentAsync(factory, other, otherMembers[2], otherMembers[0], 2_000);
        var otherDeletedRepayment = await TestData.AddGroupRepaymentAsync(
            factory, other, otherMembers[1], otherMembers[0], 1_000, deleted: true);
        var otherCategory = await AddCategoryAsync(owner, other.Id, "Ferry");
        var token = await ShareAsync(owner, group.Id);
        var otherBefore = await StoredCountsAsync(other.Id);
        using var link = new LinkClient(factory);
        var (anna, max) = (members[0].Id, members[2].Id);
        var member = anna.ToString();
        var foreign = new (HttpMethod Method, string Path, object? Body)[]
        {
            (HttpMethod.Put, $"/expenses/{otherExpense.Id}", ExpenseBody(anna)),
            (HttpMethod.Delete, $"/expenses/{otherExpense.Id}", null),
            (HttpMethod.Post, $"/expenses/{otherDeletedExpense.Id}/restore", null),
            (HttpMethod.Put, $"/repayments/{otherRepayment.Id}", RepaymentBody(max, anna)),
            (HttpMethod.Delete, $"/repayments/{otherRepayment.Id}", null),
            (HttpMethod.Post, $"/repayments/{otherDeletedRepayment.Id}/restore", null)
        };

        foreach (var (method, path, body) in foreign)
        {
            using var response = await link.SendAsync(method, token, path, body, member);

            await ShouldBeNotFoundAsync(response, $"{method} {path}");
        }

        using var expense = await link.SendAsync(
            HttpMethod.Post,
            token,
            "/expenses",
            new
            {
                payerId = otherMembers[0].Id,
                amount = 9_000,
                currency = "EUR",
                date = IsoDay(Today),
                categoryId = otherCategory,
                split = new { mode = "equal", memberIds = new[] { anna, otherMembers[1].Id } }
            },
            member);
        using var repayment = await link.SendAsync(
            HttpMethod.Post,
            token,
            "/repayments",
            RepaymentBody(otherMembers[2].Id, anna),
            member);

        await ShouldHaveErrorsAsync(
            expense,
            ("payerId", "validation.unknownMember"),
            ("categoryId", "validation.unknownCategory"),
            ("split", "validation.unknownMember"));
        await ShouldHaveErrorsAsync(repayment, ("fromMemberId", "validation.unknownMember"));
        (await StoredCountsAsync(group.Id)).ShouldBe(Empty);
        (await StoredCountsAsync(other.Id)).ShouldBe(otherBefore);
        (await DeletedEntriesAsync(other.Id))
            .ShouldBe([otherDeletedExpense.Id, otherDeletedRepayment.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task Should_CheckXsrf_When_SignedInVisitorWritesThroughLink()
    {
        using var owner = new AuthClient(factory);
        using var visitor = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        await visitor.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(owner), "Trip");
        var (anna, olya, max) = (members[0].Id, members[1].Id, members[2].Id);
        var token = await ShareAsync(owner, group.Id);
        using var link = new LinkClient(factory);

        using var withoutXsrf = await PostExpenseAsync(visitor, token, olya, xsrf: false);
        using var unknownToken = await PostExpenseAsync(visitor, ShareTokens.New(), olya, xsrf: false);

        withoutXsrf.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ReadAsync(withoutXsrf)).GetProperty("code").GetString().ShouldBe("auth.xsrf");
        await ShouldBeNotFoundAsync(unknownToken);
        (await StoredCountsAsync(group.Id)).ShouldBe(Empty);

        using var visitorWrite = await PostExpenseAsync(visitor, token, olya, xsrf: true);
        using var anonymousWrite = await link.SendAsync(HttpMethod.Post, token, "/expenses", ExpenseBody(anna), max.ToString());
        using var ownerWrite = await PostExpenseAsync(owner, token, anna, xsrf: true);

        visitorWrite.StatusCode.ShouldBe(HttpStatusCode.Created);
        ActorOf((await ReadAsync(visitorWrite)).GetProperty("createdBy")).ShouldBe(Member(olya, "Olya"));
        anonymousWrite.StatusCode.ShouldBe(HttpStatusCode.Created);
        ActorOf((await ReadAsync(anonymousWrite)).GetProperty("createdBy")).ShouldBe(Member(max, "Max"));
        ownerWrite.StatusCode.ShouldBe(HttpStatusCode.Created);
        ActorOf((await ReadAsync(ownerWrite)).GetProperty("createdBy")).ShouldBe(Member(anna, "Anna"));
        (await StoredCountsAsync(group.Id)).ShouldBe(new StoredCounts(3, 0, 3, 0));
    }

    [Fact]
    public async Task Should_Return404_When_LinkTurnedOffBeforeWrite()
    {
        using var owner = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(owner), "Trip");
        var member = members[1].Id.ToString();
        var replacedToken = await ShareAsync(owner, group.Id);
        var token = await ShareAsync(owner, group.Id);
        using var link = new LinkClient(factory);

        using var replaced = await link.SendAsync(HttpMethod.Post, replacedToken, "/expenses", ExpenseBody(members[0].Id), member);
        using var current = await link.SendAsync(HttpMethod.Post, token, "/expenses", ExpenseBody(members[0].Id), member);
        using (var off = await OwnerSendAsync(owner, HttpMethod.Delete, $"{GroupsPath}/{group.Id}/share"))
        {
            off.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        using var turnedOff = await link.SendAsync(HttpMethod.Post, token, "/expenses", ExpenseBody(members[0].Id), member);

        await ShouldBeNotFoundAsync(replaced);
        current.StatusCode.ShouldBe(HttpStatusCode.Created);
        await ShouldBeNotFoundAsync(turnedOff);
        (await StoredCountsAsync(group.Id)).ShouldBe(new StoredCounts(1, 0, 1, 0));
    }

    [Fact]
    public async Task Should_Return404_When_LinkTurnedOffAfterPolicyCheck()
    {
        await using var racing = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddTransient<IPipelineBehavior<CreateExpenseCommand, Result<GroupExpenseDto>>, TurnLinkOffBeforeHandler>()));
        using var owner = new AuthClient(racing);
        await owner.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(racing, await UserIdAsync(owner), "Trip");
        var token = await ShareAsync(owner, group.Id);
        using var link = new LinkClient(racing);

        using var response = await link.SendAsync(
            HttpMethod.Post,
            token,
            "/expenses",
            ExpenseBody(members[0].Id),
            members[1].Id.ToString());

        await ShouldBeNotFoundAsync(response);
        (await StoredTokenAsync(group.Id)).ShouldBeNull();
        (await StoredCountsAsync(group.Id)).ShouldBe(Empty);
    }

    [Fact]
    public async Task Should_AllowMemberHeader_When_Preflight()
    {
        await using var configured = factory.WithSettings(
            new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = "http://localhost:3000" });
        using var client = configured.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, LinkClient.PathOf(ShareTokens.New(), "/expenses"));
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type,x-group-member");

        using var response = await client.SendAsync(request, Ct);

        response.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe(["http://localhost:3000"]);
        string.Join(',', response.Headers.GetValues("Access-Control-Allow-Headers"))
            .ShouldContain(LinkClient.MemberHeader, Case.Insensitive);
    }

    private static object ExpenseBody(Guid payerId, long amount = 9_000) =>
        new { payerId, amount, currency = "EUR", date = IsoDay(Today) };

    private static object RepaymentBody(Guid fromMemberId, Guid toMemberId, long amount = 2_000) =>
        new { fromMemberId, toMemberId, amount, currency = "EUR", date = IsoDay(Today) };

    private static (string? Kind, Guid? MemberId, string? Name) Member(Guid memberId, string name) =>
        ("member", memberId, name);

    private static (string? Kind, Guid? MemberId, string? Name) Owner(string name) => ("owner", null, name);

    private static (string? Kind, Guid? MemberId, string? Name) ActorOf(JsonElement actor)
    {
        actor.EnumerateObject().Select(property => property.Name).ShouldBe(["kind", "memberId", "name"], ignoreOrder: true);
        var memberId = actor.GetProperty("memberId");
        return (
            actor.GetProperty("kind").GetString(),
            memberId.ValueKind == JsonValueKind.Null ? null : memberId.GetGuid(),
            actor.GetProperty("name").GetString());
    }

    private static async Task ShouldBeMemberRequiredAsync(HttpResponseMessage response, string because)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, because);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await ReadAsync(response);
        problem.GetProperty("status").GetInt32().ShouldBe(403);
        problem.GetProperty("title").GetString().ShouldBe("Forbidden");
        problem.GetProperty("code").GetString().ShouldBe("group.memberRequired", because);
    }

    private static async Task ShouldBeNotFoundAsync(HttpResponseMessage response, string? because = null)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound, because);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        (await ReadAsync(response)).GetProperty("title").GetString().ShouldBe("Not Found");
    }

    private static async Task ShouldHaveErrorsAsync(HttpResponseMessage response, params (string Field, string Code)[] expected)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReadAsync(response)).GetProperty("errors").EnumerateObject()
            .Select(error => (error.Name, error.Value.EnumerateArray().Single().GetString()!))
            .ShouldBe(expected, ignoreOrder: true);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    private static async Task<(string? Name, bool InUse)[]> InUseAsync(LinkClient link, string token)
    {
        using var response = await link.GetAsync(token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return [
            .. (await ReadAsync(response)).GetProperty("members").EnumerateArray()
                .Select(member => (member.GetProperty("name").GetString(), member.GetProperty("inUse").GetBoolean()))
        ];
    }

    private static async Task<HttpResponseMessage> PostExpenseAsync(AuthClient client, string token, Guid member, bool xsrf)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, LinkClient.PathOf(token, "/expenses"))
        {
            Content = JsonContent.Create(ExpenseBody(member))
        };
        request.Headers.Add(LinkClient.MemberHeader, member.ToString());
        return await client.Http.SendAsync(xsrf ? client.WithXsrf(request) : request, Ct);
    }

    private static async Task<HttpResponseMessage> OwnerSendAsync(
        AuthClient owner,
        HttpMethod method,
        string path,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await owner.Http.SendAsync(owner.WithXsrf(request), Ct);
    }

    private static async Task<string> ShareAsync(AuthClient owner, Guid groupId)
    {
        using var response = await OwnerSendAsync(owner, HttpMethod.Post, $"{GroupsPath}/{groupId}/share");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await ReadAsync(response)).GetProperty("shareToken").GetString()!;
    }

    private static async Task<string> AddCategoryAsync(AuthClient owner, Guid groupId, string name)
    {
        using var response = await OwnerSendAsync(owner, HttpMethod.Post, $"{GroupsPath}/{groupId}/categories", new { name });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await ReadAsync(response)).GetProperty("id").GetString()!;
    }

    private static async Task<Guid> UserIdAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync("/v1/me", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await ReadAsync(response)).GetProperty("id").GetGuid();
    }

    private static string IsoDay(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private async Task<StoredCounts> StoredCountsAsync(Guid groupId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return new StoredCounts(
            await db.GroupExpenses.CountAsync(expense => expense.GroupId == groupId, Ct),
            await db.GroupRepayments.CountAsync(repayment => repayment.GroupId == groupId, Ct),
            await db.GroupEntryEvents.CountAsync(entryEvent => entryEvent.GroupId == groupId, Ct),
            await db.GroupCategories.CountAsync(category => category.GroupId == groupId, Ct));
    }

    private async Task<Guid[]> DeletedEntriesAsync(Guid groupId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var expenses = await db.GroupExpenses.AsNoTracking()
            .Where(expense => expense.GroupId == groupId && expense.DeletedAt != null)
            .Select(expense => expense.Id)
            .ToListAsync(Ct);
        var repayments = await db.GroupRepayments.AsNoTracking()
            .Where(repayment => repayment.GroupId == groupId && repayment.DeletedAt != null)
            .Select(repayment => repayment.Id)
            .ToListAsync(Ct);
        return [.. expenses, .. repayments];
    }

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

    private async Task<string?> StoredTokenAsync(Guid groupId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Groups.AsNoTracking()
            .Where(group => group.Id == groupId)
            .Select(group => group.ShareToken)
            .SingleAsync(Ct);
    }

    private sealed record StoredCounts(int Expenses, int Repayments, int Events, int Categories);

    private sealed class TurnLinkOffBeforeHandler(AppDbContext db)
        : IPipelineBehavior<CreateExpenseCommand, Result<GroupExpenseDto>>
    {
        public async Task<Result<GroupExpenseDto>> Handle(
            CreateExpenseCommand request,
            RequestHandlerDelegate<Result<GroupExpenseDto>> next,
            CancellationToken cancellationToken)
        {
            if (request.Scope is LinkGroupScope link)
            {
                await db.Database.ExecuteSqlAsync(
                    $"""UPDATE "groups" SET "ShareToken" = NULL WHERE "ShareToken" = {link.Token}""",
                    cancellationToken);
            }

            return await next(cancellationToken);
        }
    }
}
