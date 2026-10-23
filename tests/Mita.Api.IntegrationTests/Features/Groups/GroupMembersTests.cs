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

public sealed class GroupMembersTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string GroupsPath = "/v1/groups";

    private static readonly string[] StandardKeys =
        ["groceries", "transport", "housing", "cafes", "health", "shopping", "leisure", "gifts", "other"];

    private static readonly JsonSerializerOptions StateJson = new(JsonSerializerDefaults.Web);

    private static readonly string[] MemberProperties = ["id", "name", "inUse", "linked", "linkedName", "invitedEmail"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_AddMember_AtTheEnd()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, _) = await TestData.AddGroupAsync(factory, await UserIdAsync(client), "Trip");

        using var response = await SendAsync(client, HttpMethod.Post, MembersPath(group.Id), new { name = "  Kasia " });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldBeNull();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.EnumerateObject().Select(property => property.Name).ShouldBe(MemberProperties);
        body.GetProperty("name").GetString().ShouldBe("Kasia");
        body.GetProperty("inUse").GetBoolean().ShouldBeFalse();
        body.GetProperty("linked").GetBoolean().ShouldBeFalse();
        body.GetProperty("linkedName").ValueKind.ShouldBe(JsonValueKind.Null);
        body.GetProperty("invitedEmail").ValueKind.ShouldBe(JsonValueKind.Null);

        var stored = await StoredMembersAsync(group.Id);
        stored.Select(member => member.Name).ShouldBe(["Anna", "Olya", "Max", "Kasia"]);
        stored.Select(member => member.Position).ShouldBe([0, 1, 2, 3]);
        stored[^1].Id.ShouldBe(body.GetProperty("id").GetGuid());

        using var read = await client.Http.GetAsync($"{GroupsPath}/{group.Id}", Ct);

        var members = (await read.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("members")
            .EnumerateArray().ToList();
        members.Select(member => member.GetProperty("name").GetString()).ShouldBe(["Anna", "Olya", "Max", "Kasia"]);
        members[^1].GetProperty("id").GetGuid().ShouldBe(body.GetProperty("id").GetGuid());
        members.Select(member => member.GetProperty("inUse").GetBoolean()).ShouldAllBe(inUse => !inUse);
    }

    [Fact]
    public async Task Should_AddMember_AfterTheLastPosition_When_MembersWereRemoved()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(client), "Trip");

        using var removed = await SendAsync(client, HttpMethod.Delete, MemberPath(group.Id, members[1].Id));
        using var added = await SendAsync(client, HttpMethod.Post, MembersPath(group.Id), new { name = "Kasia" });

        removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        added.StatusCode.ShouldBe(HttpStatusCode.Created);
        var stored = await StoredMembersAsync(group.Id);
        stored.Select(member => member.Name).ShouldBe(["Anna", "Max", "Kasia"]);
        stored.Select(member => member.Position).ShouldBe([0, 2, 3]);
    }

    [Fact]
    public async Task Should_RefuseMember_When_NameTakenOrGroupFull()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var (group, _) = await TestData.AddGroupAsync(factory, userId, "Trip");
        var (full, _) = await TestData.AddGroupAsync(
            factory,
            userId,
            "Full",
            Currency.EUR,
            [.. Enumerable.Range(1, Group.MaxMembers).Select(index => $"Member {index}")]);

        using var taken = await SendAsync(client, HttpMethod.Post, MembersPath(group.Id), new { name = " olya " });
        using var tooMany = await SendAsync(client, HttpMethod.Post, MembersPath(full.Id), new { name = "Extra" });

        await ShouldHaveErrorAsync(taken, "name", "validation.memberNameTaken");
        await ShouldHaveErrorAsync(tooMany, "name", "validation.tooManyMembers");
        (await StoredMembersAsync(group.Id)).Length.ShouldBe(3);
        (await StoredMembersAsync(full.Id)).Length.ShouldBe(Group.MaxMembers);
    }

    [Fact]
    public async Task Should_ReportInvalidName_When_MemberBodyInvalid()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(client), "Trip");

        using var blank = await SendAsync(client, HttpMethod.Post, MembersPath(group.Id), new { name = "   " });
        using var noBody = await SendAsync(client, HttpMethod.Post, MembersPath(group.Id));
        using var control = await SendAsync(client, HttpMethod.Post, MembersPath(group.Id), new { name = "Ma\u0007x" });
        using var tooLong = await SendAsync(
            client,
            HttpMethod.Put,
            MemberPath(group.Id, members[0].Id),
            new { name = new string('a', 41) });

        await ShouldHaveErrorAsync(blank, "name", "validation.required");
        await ShouldHaveErrorAsync(noBody, "name", "validation.required");
        await ShouldHaveErrorAsync(control, "name", "validation.invalidValue");
        await ShouldHaveErrorAsync(tooLong, "name", "validation.nameTooLong");
        (await StoredMembersAsync(group.Id)).Select(member => member.Name).ShouldBe(["Anna", "Olya", "Max"]);
    }

    [Fact]
    public async Task Should_RenameMember()
    {
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var (group, members) = await TestData.AddGroupAsync(factory, userId, "Trip");
        await TestData.AddGroupExpenseAsync(factory, group, members[0], 900, [members[0], members[2]]);
        await TestData.LinkMemberAsync(factory, members[1], userId);
        await TestData.InviteMemberAsync(factory, members[0], "anna@example.com");
        var (other, otherMembers) = await TestData.AddGroupAsync(factory, userId, "Other");

        using var renamed = await SendAsync(client, HttpMethod.Put, MemberPath(group.Id, members[1].Id), new { name = "Ola" });

        renamed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await renamed.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.EnumerateObject().Select(property => property.Name).ShouldBe(MemberProperties);
        body.GetProperty("id").GetGuid().ShouldBe(members[1].Id);
        body.GetProperty("name").GetString().ShouldBe("Ola");
        body.GetProperty("inUse").GetBoolean().ShouldBeFalse();
        body.GetProperty("linked").GetBoolean().ShouldBeTrue();
        body.GetProperty("linkedName").GetString().ShouldBe(credentials.Name);
        body.GetProperty("invitedEmail").ValueKind.ShouldBe(JsonValueKind.Null);

        using var otherCase = await SendAsync(client, HttpMethod.Put, MemberPath(group.Id, members[1].Id), new { name = " OLA " });

        otherCase.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await otherCase.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("name").GetString().ShouldBe("OLA");

        using var taken = await SendAsync(client, HttpMethod.Put, MemberPath(group.Id, members[1].Id), new { name = "Max" });

        await ShouldHaveErrorAsync(taken, "name", "validation.memberNameTaken");
        (await StoredMembersAsync(group.Id)).Select(member => member.Name).ShouldBe(["Anna", "OLA", "Max"]);

        using var inUse = await SendAsync(client, HttpMethod.Put, MemberPath(group.Id, members[0].Id), new { name = "Hanna" });

        inUse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var inUseBody = await inUse.Content.ReadFromJsonAsync<JsonElement>(Ct);
        inUseBody.GetProperty("inUse").GetBoolean().ShouldBeTrue();
        inUseBody.GetProperty("linked").GetBoolean().ShouldBeFalse();
        inUseBody.GetProperty("linkedName").ValueKind.ShouldBe(JsonValueKind.Null);
        inUseBody.GetProperty("invitedEmail").GetString().ShouldBe("anna@example.com");

        using var foreignMember = await SendAsync(
            client,
            HttpMethod.Put,
            MemberPath(group.Id, otherMembers[0].Id),
            new { name = "Hijacked" });
        using var unknownMember = await SendAsync(
            client,
            HttpMethod.Put,
            MemberPath(group.Id, Guid.NewGuid()),
            new { name = "Hijacked" });

        foreignMember.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        unknownMember.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await StoredMembersAsync(other.Id)).Select(member => member.Name).ShouldBe(["Anna", "Olya", "Max"]);
        (await StoredMembersAsync(group.Id)).Select(member => member.Name).ShouldBe(["Hanna", "OLA", "Max"]);
    }

    [Fact]
    public async Task Should_RemoveMember_OnlyWhenUnused()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var (group, members) = await TestData.AddGroupAsync(
            factory, userId, "Trip", Currency.EUR, "Payer", "Sharer", "Sender", "Receiver", "Actor", "Idle");
        await TestData.AddGroupExpenseAsync(factory, group, members[0], 1_000, [members[1]], deleted: true);
        await TestData.AddGroupRepaymentAsync(factory, group, members[2], members[3], 300);
        await AddEventAsync(group, members[4]);
        await TestData.LinkMemberAsync(factory, members[5], userId);

        foreach (var used in members[..5])
        {
            using var refused = await SendAsync(client, HttpMethod.Delete, MemberPath(group.Id, used.Id));

            await ShouldHaveErrorAsync(refused, "memberId", "validation.memberInUse");
        }

        using var before = await client.Http.GetAsync($"{GroupsPath}/{group.Id}", Ct);

        (await before.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("members").EnumerateArray()
            .Select(member => (member.GetProperty("name").GetString(), member.GetProperty("inUse").GetBoolean()))
            .ShouldBe(
                [
                    ("Payer", true), ("Sharer", true), ("Sender", true), ("Receiver", true), ("Actor", true),
                    ("Idle", false)
                ]);

        using var removed = await SendAsync(client, HttpMethod.Delete, MemberPath(group.Id, members[5].Id));

        removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await removed.Content.ReadAsStringAsync(Ct)).ShouldBeEmpty();
        using var after = await client.Http.GetAsync($"{GroupsPath}/{group.Id}", Ct);
        (await after.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("members").EnumerateArray()
            .Select(member => member.GetProperty("name").GetString())
            .ShouldBe(["Payer", "Sharer", "Sender", "Receiver", "Actor"]);
        (await StoredMembersAsync(group.Id)).Length.ShouldBe(5);

        using var again = await SendAsync(client, HttpMethod.Delete, MemberPath(group.Id, members[5].Id));

        again.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_KeepMemberInUse_When_OnlyAnEarlierExpenseStateNamesIt()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(
            factory, await UserIdAsync(client), "Trip", Currency.EUR, "Anna", "Olya", "Max", "Kasia");
        var (anna, olya, max, kasia) = (members[0], members[1], members[2], members[3]);
        var expense = await TestData.AddGroupExpenseAsync(factory, group, kasia, 9_000, [anna, olya, max]);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.GroupExpenses.Include(row => row.Shares).SingleAsync(row => row.Id == expense.Id, Ct);
            var edited = new ExpenseDetails(
                anna.Id,
                9_000,
                Currency.EUR,
                stored.Date,
                StandardGroupCategories.Default,
                null,
                [],
                null,
                1m,
                false,
                9_000,
                GroupSplitMode.Equal,
                GroupExpenseSplits.Equal(9_000, 9_000, [anna.Id, olya.Id], anna.Id));
            stored.Update(edited);
            db.GroupEntryEvents.Add(UpdatedEvent(group, GroupEntryKind.Expense, stored.Id, edited));
            await db.SaveChangesAsync(Ct);
        }

        await ShouldKeepNamedMembersAsync(client, group.Id, members, max, kasia);
    }

    [Fact]
    public async Task Should_KeepMemberInUse_When_OnlyAnEarlierRepaymentStateNamesIt()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(
            factory, await UserIdAsync(client), "Trip", Currency.EUR, "Anna", "Olya", "Max", "Kasia");
        var (anna, olya, max, kasia) = (members[0], members[1], members[2], members[3]);
        var repayment = await TestData.AddGroupRepaymentAsync(factory, group, kasia, max, 2_000);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.GroupRepayments.SingleAsync(row => row.Id == repayment.Id, Ct);
            var edited = new GroupRepaymentDetails(anna.Id, olya.Id, 2_000, Currency.EUR, stored.Date, 1m, false, 2_000, null);
            stored.Update(edited);
            db.GroupEntryEvents.Add(UpdatedEvent(group, GroupEntryKind.Repayment, stored.Id, edited));
            await db.SaveChangesAsync(Ct);
        }

        await ShouldKeepNamedMembersAsync(client, group.Id, members, max, kasia);
    }

    [Fact]
    public async Task Should_KeepExistingShares_When_MemberAdded()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(client), "Trip");
        var expense = await TestData.AddGroupExpenseAsync(factory, group, members[0], 10_000, members);
        var before = await SharesAsync(expense.Id);
        before.Length.ShouldBe(3);
        before.Sum(share => share.Amount).ShouldBe(10_000);

        using var response = await SendAsync(client, HttpMethod.Post, MembersPath(group.Id), new { name = "Kasia" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var after = await SharesAsync(expense.Id);
        after.ShouldBe(before);
        after.Select(share => share.MemberId).ShouldBe(before.Select(share => share.MemberId));
    }

    [Fact]
    public async Task Should_AddCategory()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var (group, _) = await TestData.AddGroupAsync(factory, userId, "Trip");
        var (second, _) = await TestData.AddGroupAsync(factory, userId, "Second");
        var (full, _) = await TestData.AddGroupAsync(factory, userId, "Full");
        await AddCategoriesAsync(full, Group.MaxCategories);

        using var response = await SendAsync(client, HttpMethod.Post, CategoriesPath(group.Id), new { name = "Ferry" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldBeNull();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.EnumerateObject().Select(property => property.Name).ShouldBe(
            ["id", "standard", "name"],
            ignoreOrder: true);
        body.GetProperty("standard").GetBoolean().ShouldBeFalse();
        body.GetProperty("name").GetString().ShouldBe("Ferry");
        var id = body.GetProperty("id").GetString()!;
        Guid.TryParse(id, out _).ShouldBeTrue();

        using var read = await client.Http.GetAsync($"{GroupsPath}/{group.Id}", Ct);

        var categories = (await read.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("categories")
            .EnumerateArray().ToList();
        categories.Select(category => category.GetProperty("id").GetString()).ShouldBe([.. StandardKeys, id]);
        categories[^1].GetProperty("standard").GetBoolean().ShouldBeFalse();
        categories[^1].GetProperty("name").GetString().ShouldBe("Ferry");

        foreach (var name in new[] { " ferry", "FERRY ", "Spożywcze", "SPOŻYWCZE", "groceries", "Продукты", "Продукти" })
        {
            using var refused = await SendAsync(client, HttpMethod.Post, CategoriesPath(group.Id), new { name });

            await ShouldHaveErrorAsync(refused, "name", "validation.categoryNameTaken");
        }

        (await StoredCategoryNamesAsync(group.Id)).ShouldBe(["Ferry"]);

        using var otherGroup = await SendAsync(client, HttpMethod.Post, CategoriesPath(second.Id), new { name = "Ferry" });
        using var tooMany = await SendAsync(client, HttpMethod.Post, CategoriesPath(full.Id), new { name = "One more" });
        using var invalid = await SendAsync(client, HttpMethod.Post, CategoriesPath(group.Id), new { name = " " });

        otherGroup.StatusCode.ShouldBe(HttpStatusCode.Created);
        await ShouldHaveErrorAsync(tooMany, "name", "validation.tooManyCategories");
        await ShouldHaveErrorAsync(invalid, "name", "validation.required");
        (await StoredCategoryNamesAsync(full.Id)).Length.ShouldBe(Group.MaxCategories);
    }

    [Fact]
    public async Task Should_AddOneMember_When_SameNameSentConcurrently()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, _) = await TestData.AddGroupAsync(factory, await UserIdAsync(client), "Trip");

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 6).Select(_ => SendAsync(client, HttpMethod.Post, MembersPath(group.Id), new { name = "Kasia" })));

        try
        {
            responses.Count(response => response.StatusCode == HttpStatusCode.Created).ShouldBe(1);
            foreach (var refused in responses.Where(response => response.StatusCode != HttpStatusCode.Created))
            {
                await ShouldHaveErrorAsync(refused, "name", "validation.memberNameTaken");
            }
        }
        finally
        {
            DisposeAll(responses);
        }

        (await StoredMembersAsync(group.Id)).Select(member => member.Name).ShouldBe(["Anna", "Olya", "Max", "Kasia"]);
    }

    [Fact]
    public async Task Should_StopAtTheLimit_When_MembersAddedConcurrently()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, _) = await TestData.AddGroupAsync(
            factory,
            await UserIdAsync(client),
            "Almost full",
            Currency.EUR,
            [.. Enumerable.Range(1, Group.MaxMembers - 1).Select(index => $"Member {index}")]);

        var responses = await Task.WhenAll(
            Enumerable.Range(1, 6).Select(index =>
                SendAsync(client, HttpMethod.Post, MembersPath(group.Id), new { name = $"New {index}" })));

        try
        {
            responses.Count(response => response.StatusCode == HttpStatusCode.Created).ShouldBe(1);
            foreach (var refused in responses.Where(response => response.StatusCode != HttpStatusCode.Created))
            {
                await ShouldHaveErrorAsync(refused, "name", "validation.tooManyMembers");
            }
        }
        finally
        {
            DisposeAll(responses);
        }

        (await StoredMembersAsync(group.Id)).Length.ShouldBe(Group.MaxMembers);
    }

    [Fact]
    public async Task Should_AddOneCategory_When_SameNameSentConcurrently()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, _) = await TestData.AddGroupAsync(factory, await UserIdAsync(client), "Trip");

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 6).Select(_ => SendAsync(client, HttpMethod.Post, CategoriesPath(group.Id), new { name = "Ferry" })));

        try
        {
            responses.Count(response => response.StatusCode == HttpStatusCode.Created).ShouldBe(1);
            foreach (var refused in responses.Where(response => response.StatusCode != HttpStatusCode.Created))
            {
                await ShouldHaveErrorAsync(refused, "name", "validation.categoryNameTaken");
            }
        }
        finally
        {
            DisposeAll(responses);
        }

        (await StoredCategoryNamesAsync(group.Id)).ShouldBe(["Ferry"]);
    }

    [Fact]
    public async Task Should_StopAtTheLimit_When_CategoriesAddedConcurrently()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, _) = await TestData.AddGroupAsync(factory, await UserIdAsync(client), "Trip");
        await AddCategoriesAsync(group, Group.MaxCategories - 1);

        var responses = await Task.WhenAll(
            Enumerable.Range(1, 6).Select(index =>
                SendAsync(client, HttpMethod.Post, CategoriesPath(group.Id), new { name = $"New {index}" })));

        try
        {
            responses.Count(response => response.StatusCode == HttpStatusCode.Created).ShouldBe(1);
            foreach (var refused in responses.Where(response => response.StatusCode != HttpStatusCode.Created))
            {
                await ShouldHaveErrorAsync(refused, "name", "validation.tooManyCategories");
            }
        }
        finally
        {
            DisposeAll(responses);
        }

        (await StoredCategoryNamesAsync(group.Id)).Length.ShouldBe(Group.MaxCategories);
    }

    [Fact]
    public async Task Should_Return404_When_GroupForeign()
    {
        using var owner = new AuthClient(factory);
        using var intruder = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        await intruder.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(owner), "Trip");
        var unknown = Guid.NewGuid();

        using var add = await SendAsync(intruder, HttpMethod.Post, MembersPath(group.Id), new { name = "Kasia" });
        using var rename = await SendAsync(intruder, HttpMethod.Put, MemberPath(group.Id, members[1].Id), new { name = "Hijacked" });
        using var remove = await SendAsync(intruder, HttpMethod.Delete, MemberPath(group.Id, members[2].Id));
        using var category = await SendAsync(intruder, HttpMethod.Post, CategoriesPath(group.Id), new { name = "Ferry" });
        using var unknownAdd = await SendAsync(owner, HttpMethod.Post, MembersPath(unknown), new { name = "Kasia" });
        using var unknownRename = await SendAsync(owner, HttpMethod.Put, MemberPath(unknown, members[1].Id), new { name = "Ola" });
        using var unknownRemove = await SendAsync(owner, HttpMethod.Delete, MemberPath(unknown, members[2].Id));
        using var unknownCategory = await SendAsync(owner, HttpMethod.Post, CategoriesPath(unknown), new { name = "Ferry" });
        using var notAGuid = await SendAsync(owner, HttpMethod.Post, $"{GroupsPath}/not-a-guid/members", new { name = "Kasia" });
        using var memberNotAGuid = await SendAsync(owner, HttpMethod.Delete, $"{MembersPath(group.Id)}/not-a-guid");
        using var invalidBody = await SendAsync(owner, HttpMethod.Post, MembersPath(unknown), new { name = " " });

        foreach (var response in new[]
                 {
                     add, rename, remove, category, unknownAdd, unknownRename, unknownRemove, unknownCategory, notAGuid,
                     memberNotAGuid
                 })
        {
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        await ShouldHaveErrorAsync(invalidBody, "name", "validation.required");
        (await StoredMembersAsync(group.Id)).Select(member => (member.Name, member.Position))
            .ShouldBe([("Anna", 0), ("Olya", 1), ("Max", 2)]);
        (await StoredCategoryNamesAsync(group.Id)).ShouldBeEmpty();
    }

    private static string MembersPath(Guid groupId) => $"{GroupsPath}/{groupId}/members";

    private static string MemberPath(Guid groupId, Guid memberId) => $"{MembersPath(groupId)}/{memberId}";

    private static string CategoriesPath(Guid groupId) => $"{GroupsPath}/{groupId}/categories";

    private static GroupEntryEvent UpdatedEvent(Group group, GroupEntryKind kind, Guid entryId, object state) =>
        GroupEntryEvent.Create(
            group.Id,
            kind,
            entryId,
            GroupEntryAction.Updated,
            DateTimeOffset.UtcNow,
            GroupActor.Owner(group.OwnerId),
            JsonSerializer.Serialize(state, state.GetType(), StateJson));

    private async Task ShouldKeepNamedMembersAsync(
        AuthClient client,
        Guid groupId,
        GroupMember[] members,
        params GroupMember[] namedOnlyInHistory)
    {
        (await SharesOrSidesAsync(groupId)).ShouldNotContain(memberId => namedOnlyInHistory.Any(member => member.Id == memberId));

        using var read = await client.Http.GetAsync($"{GroupsPath}/{groupId}", Ct);

        (await read.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("members").EnumerateArray()
            .Select(member => (member.GetProperty("id").GetGuid(), member.GetProperty("inUse").GetBoolean()))
            .ShouldBe([.. members.Select(member => (member.Id, true))]);

        foreach (var named in namedOnlyInHistory)
        {
            using var refused = await SendAsync(client, HttpMethod.Delete, MemberPath(groupId, named.Id));

            await ShouldHaveErrorAsync(refused, "memberId", "validation.memberInUse");
        }

        (await StoredMembersAsync(groupId)).Length.ShouldBe(members.Length);
    }

    private async Task<Guid[]> SharesOrSidesAsync(Guid groupId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var expenses = await db.GroupExpenses.AsNoTracking()
            .Where(row => row.GroupId == groupId)
            .Select(row => row.PayerId)
            .Concat(db.GroupExpenseShares.Where(row => row.GroupId == groupId).Select(row => row.MemberId))
            .ToListAsync(Ct);
        var repayments = await db.GroupRepayments.AsNoTracking()
            .Where(row => row.GroupId == groupId)
            .Select(row => new { row.FromMemberId, row.ToMemberId })
            .ToListAsync(Ct);
        return [.. expenses, .. repayments.SelectMany(row => new[] { row.FromMemberId, row.ToMemberId })];
    }

    private static Task<HttpResponseMessage> SendAsync(
        AuthClient client,
        HttpMethod method,
        string path,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.Http.SendAsync(client.WithXsrf(request), Ct);
    }

    private static void DisposeAll(IEnumerable<HttpResponseMessage> responses)
    {
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    private async Task AddEventAsync(Group group, GroupMember actor)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.GroupEntryEvents.Add(
            GroupEntryEvent.Create(
                group.Id,
                GroupEntryKind.Expense,
                Guid.NewGuid(),
                GroupEntryAction.Updated,
                DateTimeOffset.UtcNow,
                GroupActor.Member(actor.Id),
                "{}"));
        await db.SaveChangesAsync(Ct);
    }

    private async Task AddCategoriesAsync(Group group, int count)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.GroupCategories.AddRange(
            Enumerable.Range(1, count).Select(index => GroupCategory.Create(group.Id, $"Category {index}")));
        await db.SaveChangesAsync(Ct);
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

    private async Task<string[]> StoredCategoryNamesAsync(Guid groupId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.GroupCategories.AsNoTracking()
            .Where(row => row.GroupId == groupId)
            .OrderBy(row => row.CreatedAt)
            .ThenBy(row => row.Id)
            .Select(row => row.Name)
            .ToArrayAsync(Ct);
    }

    private async Task<(Guid MemberId, long Amount, long GroupAmount)[]> SharesAsync(Guid expenseId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var shares = await db.GroupExpenseShares.AsNoTracking()
            .Where(row => row.ExpenseId == expenseId)
            .OrderBy(row => row.MemberId)
            .ToListAsync(Ct);
        return [.. shares.Select(share => (share.MemberId, share.Amount, share.GroupAmount))];
    }

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
