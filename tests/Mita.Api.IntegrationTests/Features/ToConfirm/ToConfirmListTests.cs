using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Categories;
using Mita.Domain.Groups;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.ToConfirm;

public sealed class ToConfirmListTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string ToConfirmPath = "/v1/to-confirm";

    private const string GroupsPath = "/v1/groups";

    private static readonly string[] ItemProperties =
    [
        "kind", "entryId", "groupId", "groupName", "groupCurrency", "change", "version", "at", "actor", "current",
        "previous", "other", "lines", "suggestedAccountId"
    ];

    private static readonly string[] ExpenseTermProperties =
        ["amount", "currency", "date", "share", "categoryId", "tags", "description"];

    private static readonly string[] PaidTermProperties =
        ["direction", "memberId", "amount", "currency", "date", "groupAmount", "covers"];

    private static readonly string[] ReceivedTermProperties = ["direction", "memberId", "amount", "currency", "date"];

    private static readonly string[] LineProperties = ["expenseId", "description", "amount", "currency", "suggestion"];

    private static readonly string[] SuggestionProperties = ["categoryId", "newCategory", "title", "tags"];

    private static readonly string[] DinnerTags = ["dinner"];

    private static readonly string[] FoodTags = ["food"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Should_ListExpensesThePlayerPaid()
    {
        using var owner = new AuthClient(factory);
        using var user = new AuthClient(factory);
        var trip = await SetUpAsync(owner, user);

        var expenseId = await CreateExpenseAsync(owner, trip.Group.Id, new
        {
            payerId = trip.Anna.Id,
            amount = 12_000,
            currency = "EUR",
            date = IsoDay(Today),
            categoryId = "cafes",
            tags = DinnerTags,
            description = "Dinner"
        });

        var list = await ListAsync(user);

        list.GetProperty("total").GetInt32().ShouldBe(1);
        var item = list.GetProperty("items").EnumerateArray().Single();
        Names(item).ShouldBe(ItemProperties, ignoreOrder: true);
        item.GetProperty("kind").GetString().ShouldBe("expense");
        item.GetProperty("entryId").GetGuid().ShouldBe(expenseId);
        item.GetProperty("groupId").GetGuid().ShouldBe(trip.Group.Id);
        item.GetProperty("groupName").GetString().ShouldBe("Trip");
        item.GetProperty("groupCurrency").GetString().ShouldBe("EUR");
        item.GetProperty("change").GetString().ShouldBe("new");
        var newest = await NewestEventAsync(owner, trip.Group.Id, "expenses", expenseId);
        item.GetProperty("version").GetString().ShouldBe(VersionOf(newest, trip.Anna));
        item.GetProperty("at").GetString().ShouldBe(newest.GetProperty("at").GetString());
        var actor = item.GetProperty("actor");
        actor.GetProperty("kind").GetString().ShouldBe("owner");
        actor.GetProperty("memberId").ValueKind.ShouldBe(JsonValueKind.Null);
        actor.GetProperty("name").GetString().ShouldBe(trip.OwnerName);
        var current = item.GetProperty("current");
        Names(current).ShouldBe(ExpenseTermProperties, ignoreOrder: true);
        current.GetProperty("amount").GetInt64().ShouldBe(12_000);
        current.GetProperty("currency").GetString().ShouldBe("EUR");
        current.GetProperty("date").GetString().ShouldBe(IsoDay(Today));
        current.GetProperty("share").GetInt64().ShouldBe(4_000);
        current.GetProperty("categoryId").GetString().ShouldBe("cafes");
        Strings(current.GetProperty("tags")).ShouldBe(["dinner"]);
        current.GetProperty("description").GetString().ShouldBe("Dinner");
        item.GetProperty("previous").ValueKind.ShouldBe(JsonValueKind.Null);
        item.GetProperty("other").GetInt64().ShouldBe(8_000);
        var line = item.GetProperty("lines").EnumerateArray().Single();
        Names(line).ShouldBe(LineProperties, ignoreOrder: true);
        line.GetProperty("expenseId").GetGuid().ShouldBe(expenseId);
        line.GetProperty("description").GetString().ShouldBe("Dinner");
        line.GetProperty("amount").GetInt64().ShouldBe(4_000);
        line.GetProperty("currency").GetString().ShouldBe("EUR");
        var suggestion = line.GetProperty("suggestion");
        Names(suggestion).ShouldBe(SuggestionProperties, ignoreOrder: true);
        suggestion.GetProperty("categoryId").GetGuid().ShouldBe(await ExpenseCategoryIdAsync(user, "Cafes"));
        suggestion.GetProperty("newCategory").ValueKind.ShouldBe(JsonValueKind.Null);
        suggestion.GetProperty("title").GetString().ShouldBe("Dinner");
        Strings(suggestion.GetProperty("tags")).ShouldBe(["dinner"]);
        item.GetProperty("suggestedAccountId").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Should_ListRepayments_BothWays()
    {
        using var owner = new AuthClient(factory);
        using var user = new AuthClient(factory);
        var trip = await SetUpAsync(owner, user);
        var market = await CreateExpenseAsync(owner, trip.Group.Id, new
        {
            payerId = trip.Olya.Id,
            amount = 9_000,
            currency = "EUR",
            date = IsoDay(Today),
            categoryId = "groceries",
            tags = FoodTags,
            description = "Market"
        });

        var received = await CreateRepaymentAsync(owner, trip.Group.Id, Repayment(trip.Olya, trip.Anna, 4_000));
        var paid = await CreateRepaymentAsync(owner, trip.Group.Id, Repayment(trip.Anna, trip.Olya, 3_000));

        var items = (await ListAsync(user)).GetProperty("items").EnumerateArray().ToList();
        items.Select(EntryId).ShouldBe([paid, received]);
        items.ShouldAllBe(item => item.GetProperty("kind").GetString() == "repayment");
        items.ShouldAllBe(item => item.GetProperty("change").GetString() == "new");

        var receivedTerms = items[1].GetProperty("current");
        Names(receivedTerms).ShouldBe(ReceivedTermProperties);
        receivedTerms.GetProperty("direction").GetString().ShouldBe("received");
        receivedTerms.GetProperty("memberId").GetGuid().ShouldBe(trip.Olya.Id);
        receivedTerms.GetProperty("amount").GetInt64().ShouldBe(4_000);
        receivedTerms.GetProperty("currency").GetString().ShouldBe("EUR");
        receivedTerms.GetProperty("date").GetString().ShouldBe(IsoDay(Today));
        items[1].GetProperty("lines").GetArrayLength().ShouldBe(0);
        items[1].GetProperty("other").GetInt64().ShouldBe(4_000);

        var paidTerms = items[0].GetProperty("current");
        Names(paidTerms).ShouldBe(PaidTermProperties);
        paidTerms.GetProperty("direction").GetString().ShouldBe("paid");
        paidTerms.GetProperty("memberId").GetGuid().ShouldBe(trip.Olya.Id);
        paidTerms.GetProperty("amount").GetInt64().ShouldBe(3_000);
        paidTerms.GetProperty("groupAmount").GetInt64().ShouldBe(3_000);
        var cover = paidTerms.GetProperty("covers").EnumerateArray().Single();
        Names(cover).ShouldBe(["expenseId", "groupAmount"], ignoreOrder: true);
        cover.GetProperty("expenseId").GetGuid().ShouldBe(market);
        cover.GetProperty("groupAmount").GetInt64().ShouldBe(3_000);
        items[0].GetProperty("other").GetInt64().ShouldBe(0);
        var line = items[0].GetProperty("lines").EnumerateArray().Single();
        line.GetProperty("expenseId").GetGuid().ShouldBe(market);
        line.GetProperty("description").GetString().ShouldBe("Market");
        line.GetProperty("amount").GetInt64().ShouldBe(3_000);
        line.GetProperty("currency").GetString().ShouldBe("EUR");
        var suggestion = line.GetProperty("suggestion");
        suggestion.GetProperty("categoryId").GetGuid().ShouldBe(await ExpenseCategoryIdAsync(user, "Groceries"));
        suggestion.GetProperty("newCategory").ValueKind.ShouldBe(JsonValueKind.Null);
        suggestion.GetProperty("title").GetString().ShouldBe("Market");
        Strings(suggestion.GetProperty("tags")).ShouldBe(["food"]);
    }

    [Fact]
    public async Task Should_SplitRepaymentLines_InTheRepaymentCurrency()
    {
        using var owner = new AuthClient(factory);
        using var user = new AuthClient(factory);
        var trip = await SetUpAsync(owner, user);
        var market = await CreateExpenseAsync(owner, trip.Group.Id, new
        {
            payerId = trip.Olya.Id,
            amount = 9_000,
            currency = "EUR",
            date = IsoDay(Today),
            description = "Market"
        });

        var paid = await CreateRepaymentAsync(owner, trip.Group.Id, PaidInPln(trip, 0.25m));

        var item = (await ListAsync(user)).GetProperty("items").EnumerateArray().Single();
        item.GetProperty("entryId").GetGuid().ShouldBe(paid);
        item.GetProperty("current").GetProperty("amount").GetInt64().ShouldBe(20_000);
        item.GetProperty("current").GetProperty("currency").GetString().ShouldBe("PLN");
        item.GetProperty("current").GetProperty("groupAmount").GetInt64().ShouldBe(5_000);
        var cover = item.GetProperty("current").GetProperty("covers").EnumerateArray().Single();
        cover.GetProperty("expenseId").GetGuid().ShouldBe(market);
        cover.GetProperty("groupAmount").GetInt64().ShouldBe(3_000);
        var line = item.GetProperty("lines").EnumerateArray().Single();
        line.GetProperty("amount").GetInt64().ShouldBe(12_000);
        line.GetProperty("currency").GetString().ShouldBe("PLN");
        line.GetProperty("suggestion").GetProperty("categoryId").GetGuid()
            .ShouldBe(await ExpenseCategoryIdAsync(user, "Other"));
        item.GetProperty("other").GetInt64().ShouldBe(8_000);
    }

    [Fact]
    public async Task Should_IgnoreOtherMembersExpenses()
    {
        using var owner = new AuthClient(factory);
        using var user = new AuthClient(factory);
        var trip = await SetUpAsync(owner, user);

        await CreateExpenseAsync(owner, trip.Group.Id, new
        {
            payerId = trip.Olya.Id,
            amount = 9_000,
            currency = "EUR",
            date = IsoDay(Today)
        });
        var forOlya = await CreateExpenseAsync(owner, trip.Group.Id, new
        {
            payerId = trip.Anna.Id,
            amount = 300,
            currency = "EUR",
            date = IsoDay(Today),
            split = new { mode = "equal", memberIds = new[] { trip.Olya.Id } }
        });

        var item = (await ListAsync(user)).GetProperty("items").EnumerateArray().Single();
        item.GetProperty("entryId").GetGuid().ShouldBe(forOlya);
        item.GetProperty("current").GetProperty("share").GetInt64().ShouldBe(0);
        item.GetProperty("lines").GetArrayLength().ShouldBe(0);
        item.GetProperty("other").GetInt64().ShouldBe(300);
    }

    [Fact]
    public async Task Should_SuggestCategories()
    {
        using var owner = new AuthClient(factory);
        using var user = new AuthClient(factory);
        var trip = await SetUpAsync(owner, user);
        using (var deleted = await SendAsync(
            user,
            HttpMethod.Delete,
            $"/v1/categories/{await ExpenseCategoryIdAsync(user, "Health")}"))
        {
            deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        var ferry = await TestData.AddCategoryAsync(factory, trip.UserId, CategoryKind.Expense, "ferry");
        await TestData.AddCategoryAsync(factory, trip.UserId, CategoryKind.Income, "Boat");
        var ferryGroup = await AddGroupCategoryAsync(owner, trip.Group.Id, "Ferry");
        var boatGroup = await AddGroupCategoryAsync(owner, trip.Group.Id, "Boat");
        var pharmacy = await CreateExpenseAsync(owner, trip.Group.Id, Paid(trip.Anna, "health", "Pharmacy"));
        var crossing = await CreateExpenseAsync(owner, trip.Group.Id, Paid(trip.Anna, ferryGroup, "Crossing"));
        var boat = await CreateExpenseAsync(owner, trip.Group.Id, Paid(trip.Anna, boatGroup, null));

        var suggestions = (await ListAsync(user)).GetProperty("items").EnumerateArray()
            .ToDictionary(EntryId, item => item.GetProperty("lines")[0].GetProperty("suggestion"));

        suggestions[pharmacy].GetProperty("categoryId").GetGuid().ShouldBe(await ExpenseCategoryIdAsync(user, "Other"));
        suggestions[pharmacy].GetProperty("newCategory").ValueKind.ShouldBe(JsonValueKind.Null);
        suggestions[pharmacy].GetProperty("title").GetString().ShouldBe("Pharmacy");
        suggestions[crossing].GetProperty("categoryId").GetGuid().ShouldBe(ferry.Id);
        suggestions[crossing].GetProperty("newCategory").ValueKind.ShouldBe(JsonValueKind.Null);
        suggestions[crossing].GetProperty("title").GetString().ShouldBe("Crossing");
        suggestions[boat].GetProperty("categoryId").ValueKind.ShouldBe(JsonValueKind.Null);
        suggestions[boat].GetProperty("newCategory").GetString().ShouldBe("Boat");
        suggestions[boat].GetProperty("title").GetString().ShouldBe("Trip");
        Strings(suggestions[boat].GetProperty("tags")).ShouldBe(["dinner"]);
    }

    [Fact]
    public async Task Should_SuggestOther_When_GroupCategoryIsNamedLikeTransferCategory()
    {
        using var owner = new AuthClient(factory);
        using var user = new AuthClient(factory);
        var trip = await SetUpAsync(owner, user);
        var transfers = await AddGroupCategoryAsync(owner, trip.Group.Id, "Transfers");
        var expenseId = await CreateExpenseAsync(owner, trip.Group.Id, Paid(trip.Anna, transfers, "Taxi"));
        var account = await TestData.AddAccountAsync(factory, trip.UserId, Currency.PLN, 50_000);

        var item = (await ListAsync(user)).GetProperty("items").EnumerateArray().Single(item => EntryId(item) == expenseId);
        var suggestion = item.GetProperty("lines")[0].GetProperty("suggestion");

        suggestion.GetProperty("categoryId").GetGuid().ShouldBe(await ExpenseCategoryIdAsync(user, "Other"));
        suggestion.GetProperty("newCategory").ValueKind.ShouldBe(JsonValueKind.Null);
        using var confirmed = await SendAsync(
            user,
            HttpMethod.Post,
            $"{ToConfirmPath}/expenses/{expenseId}/confirm",
            new
            {
                version = item.GetProperty("version").GetString(),
                accountId = account.Id,
                rate = 4.3m,
                lines = new[]
                {
                    new
                    {
                        categoryId = suggestion.GetProperty("categoryId").GetGuid(),
                        newCategory = (string?)null,
                        title = suggestion.GetProperty("title").GetString(),
                        tags = Strings(suggestion.GetProperty("tags"))
                    }
                }
            });
        confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Should_ShowPastEntries_When_LinkedLate()
    {
        using var owner = new AuthClient(factory);
        using var user = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        await user.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(owner));
        var (anna, olya) = (members[0], members[1]);
        var expense = await TestData.AddGroupExpenseAsync(factory, group, anna, 9_000, members, IsoDay(Today.AddDays(-2)));
        var repayment = await TestData.AddGroupRepaymentAsync(factory, group, olya, anna, 1_000, IsoDay(Today.AddDays(-1)));
        (await ListAsync(user)).GetProperty("total").GetInt32().ShouldBe(0);

        await TestData.LinkMemberAsync(factory, anna, await UserIdAsync(user));

        var items = (await ListAsync(user)).GetProperty("items").EnumerateArray().ToList();
        items.Select(EntryId).ShouldBe([repayment.Id, expense.Id]);
        items.Select(item => item.GetProperty("kind").GetString()).ShouldBe(["repayment", "expense"]);
        items[0].GetProperty("current").GetProperty("direction").GetString().ShouldBe("received");
        items[1].GetProperty("current").GetProperty("share").GetInt64().ShouldBe(3_000);

        await TestData.UnlinkMemberAsync(factory, anna);

        var after = await ListAsync(user);
        after.GetProperty("total").GetInt32().ShouldBe(0);
        after.GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Should_HideDeletedEntriesNeverConfirmed()
    {
        using var owner = new AuthClient(factory);
        using var user = new AuthClient(factory);
        var trip = await SetUpAsync(owner, user);
        await TestData.AddGroupExpenseAsync(factory, trip.Group, trip.Anna, 3_000, trip.Members, deleted: true);
        await TestData.AddGroupRepaymentAsync(factory, trip.Group, trip.Anna, trip.Olya, 1_000, deleted: true);
        var removed = await CreateExpenseAsync(owner, trip.Group.Id, Paid(trip.Anna, "cafes", "Lunch"));
        using (var deleted = await SendAsync(owner, HttpMethod.Delete, $"{GroupsPath}/{trip.Group.Id}/expenses/{removed}"))
        {
            deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        var live = await TestData.AddGroupExpenseAsync(factory, trip.Group, trip.Anna, 3_000, trip.Members);

        var list = await ListAsync(user);
        list.GetProperty("total").GetInt32().ShouldBe(1);
        list.GetProperty("items").EnumerateArray().Select(EntryId).ShouldBe([live.Id]);
    }

    [Fact]
    public async Task Should_ListOnlyOwnItems()
    {
        using var owner = new AuthClient(factory);
        using var user = new AuthClient(factory);
        using var other = new AuthClient(factory);
        using var stranger = new AuthClient(factory);
        var trip = await SetUpAsync(owner, user);
        await other.RegisterNewAsync(Ct);
        await stranger.RegisterNewAsync(Ct);
        await TestData.LinkMemberAsync(factory, trip.Olya, await UserIdAsync(other));
        var (elsewhere, elsewhereMembers) = await TestData.AddGroupAsync(factory, trip.OwnerId, "Elsewhere");
        await TestData.AddGroupExpenseAsync(factory, elsewhere, elsewhereMembers[0], 3_000, elsewhereMembers);
        var annaPaid = await TestData.AddGroupExpenseAsync(factory, trip.Group, trip.Anna, 9_000, trip.Members);
        var olyaPaid = await TestData.AddGroupExpenseAsync(factory, trip.Group, trip.Olya, 6_000, trip.Members);
        await TestData.AddGroupExpenseAsync(factory, trip.Group, trip.Max, 3_000, trip.Members);
        var olyaToMax = await TestData.AddGroupRepaymentAsync(factory, trip.Group, trip.Olya, trip.Max, 1_000);
        var token = await ShareAsync(owner, trip.Group.Id);

        (await ListAsync(user)).GetProperty("items").EnumerateArray().Select(EntryId).ShouldBe([annaPaid.Id]);
        (await ListAsync(other)).GetProperty("items").EnumerateArray().Select(EntryId).ShouldBe([olyaToMax.Id, olyaPaid.Id]);
        (await ListAsync(owner)).GetProperty("total").GetInt32().ShouldBe(0);
        (await ListAsync(stranger)).GetProperty("total").GetInt32().ShouldBe(0);

        using var anonymous = factory.CreateClient();
        using var unauthorized = await anonymous.GetAsync(ToConfirmPath, Ct);
        unauthorized.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using var link = new LinkClient(factory);
        using var throughLink = await link.GetAsync(token, "/to-confirm");
        throughLink.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_PageNewestFirst()
    {
        using var owner = new AuthClient(factory);
        using var user = new AuthClient(factory);
        var trip = await SetUpAsync(owner, user);
        var first = await TestData.AddGroupExpenseAsync(factory, trip.Group, trip.Anna, 3_000, trip.Members, IsoDay(Today));
        var second = await TestData.AddGroupExpenseAsync(factory, trip.Group, trip.Anna, 3_000, trip.Members, IsoDay(Today.AddDays(-3)));
        var third = await TestData.AddGroupRepaymentAsync(factory, trip.Group, trip.Max, trip.Anna, 1_000, IsoDay(Today.AddDays(-9)));

        var page = await ListAsync(user, "?page=1&pageSize=2");

        page.GetProperty("total").GetInt32().ShouldBe(3);
        page.GetProperty("page").GetInt32().ShouldBe(1);
        page.GetProperty("pageSize").GetInt32().ShouldBe(2);
        var items = page.GetProperty("items").EnumerateArray().ToList();
        items.Select(EntryId).ShouldBe([third.Id, second.Id]);
        DateTimeOffset.Parse(items[0].GetProperty("at").GetString()!, CultureInfo.InvariantCulture)
            .ShouldBeGreaterThan(DateTimeOffset.Parse(items[1].GetProperty("at").GetString()!, CultureInfo.InvariantCulture));
        (await ListAsync(user, "?page=2&pageSize=2")).GetProperty("items").EnumerateArray().Select(EntryId)
            .ShouldBe([first.Id]);
        (await ListAsync(user)).GetProperty("pageSize").GetInt32().ShouldBe(100);

        foreach (var query in new[] { "?pageSize=101", "?pageSize=0", "?page=0" })
        {
            using var response = await user.Http.GetAsync(ToConfirmPath + query, Ct);
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, query);
        }
    }

    [Fact]
    public async Task Should_ListChangesAndRemovals_When_ConfirmedEntryChanges()
    {
        using var owner = new AuthClient(factory);
        using var user = new AuthClient(factory);
        var trip = await SetUpAsync(owner, user);
        var account = await TestData.AddAccountAsync(factory, trip.UserId, openingBalance: 50_000);
        var restaurants = await TestData.AddCategoryAsync(factory, trip.UserId, CategoryKind.Expense, "Restaurants");
        var work = await TestData.AddTagAsync(factory, trip.UserId, "work");
        var expenseId = await CreateExpenseAsync(owner, trip.Group.Id, Paid(trip.Anna, "cafes", "Dinner", 12_000));
        var confirmed =
            $$"""{"tags": ["dinner"], "description": "Dinner", "share": 4000, "categoryId": "cafes", "date": "{{IsoDay(Today)}}", "currency": "EUR", "amount": 12000}""";
        var booked = await TestData.AddTransactionAsync(factory, account, restaurants, 4_000, IsoDay(Today), "Team dinner");
        await TestData.TagTransactionAsync(factory, booked, work);
        await SeedConfirmationAsync(trip, GroupEntryKind.Expense, expenseId, confirmed, account.Id, (booked.Id, expenseId), -8_000);

        (await ListAsync(user)).GetProperty("total").GetInt32().ShouldBe(0);

        await UpdateExpenseAsync(owner, trip.Group.Id, expenseId, Paid(trip.Anna, "cafes", "Dinner", 15_000));

        var changed = (await ListAsync(user)).GetProperty("items").EnumerateArray().Single();
        changed.GetProperty("change").GetString().ShouldBe("changed");
        changed.GetProperty("version").GetString()
            .ShouldBe(VersionOf(await NewestEventAsync(owner, trip.Group.Id, "expenses", expenseId), trip.Anna));
        var previous = changed.GetProperty("previous");
        Names(previous).ShouldBe(ExpenseTermProperties, ignoreOrder: true);
        previous.GetProperty("amount").GetInt64().ShouldBe(12_000);
        previous.GetProperty("share").GetInt64().ShouldBe(4_000);
        Strings(previous.GetProperty("tags")).ShouldBe(["dinner"]);
        changed.GetProperty("current").GetProperty("amount").GetInt64().ShouldBe(15_000);
        changed.GetProperty("current").GetProperty("share").GetInt64().ShouldBe(5_000);
        changed.GetProperty("other").GetInt64().ShouldBe(10_000);
        changed.GetProperty("suggestedAccountId").GetGuid().ShouldBe(account.Id);
        var line = changed.GetProperty("lines").EnumerateArray().Single();
        line.GetProperty("amount").GetInt64().ShouldBe(5_000);
        var suggestion = line.GetProperty("suggestion");
        suggestion.GetProperty("categoryId").GetGuid().ShouldBe(restaurants.Id);
        suggestion.GetProperty("newCategory").ValueKind.ShouldBe(JsonValueKind.Null);
        suggestion.GetProperty("title").GetString().ShouldBe("Team dinner");
        Strings(suggestion.GetProperty("tags")).ShouldBe(["work"]);

        await UpdateExpenseAsync(owner, trip.Group.Id, expenseId, Paid(trip.Olya, "cafes", "Dinner", 15_000));

        var removed = (await ListAsync(user)).GetProperty("items").EnumerateArray().Single();
        removed.GetProperty("entryId").GetGuid().ShouldBe(expenseId);
        removed.GetProperty("change").GetString().ShouldBe("removed");
        removed.GetProperty("current").ValueKind.ShouldBe(JsonValueKind.Null);
        removed.GetProperty("previous").GetProperty("amount").GetInt64().ShouldBe(12_000);
        removed.GetProperty("lines").GetArrayLength().ShouldBe(0);
        removed.GetProperty("other").GetInt64().ShouldBe(0);
    }

    [Fact]
    public async Task Should_HideRejectedEntry_UntilItChanges()
    {
        using var owner = new AuthClient(factory);
        using var user = new AuthClient(factory);
        var trip = await SetUpAsync(owner, user);
        var repaymentId = await CreateRepaymentAsync(owner, trip.Group.Id, Repayment(trip.Olya, trip.Anna, 4_000));
        var rejected =
            $$"""{"date": "{{IsoDay(Today)}}", "amount": 4000, "currency": "EUR", "memberId": "{{trip.Olya.Id}}", "direction": "received"}""";
        await SeedConfirmationAsync(trip, GroupEntryKind.Repayment, repaymentId, rejected, null, null, null);

        (await ListAsync(user)).GetProperty("total").GetInt32().ShouldBe(0);

        using (var edited = await SendAsync(
            owner,
            HttpMethod.Put,
            $"{GroupsPath}/{trip.Group.Id}/repayments/{repaymentId}",
            Repayment(trip.Olya, trip.Anna, 5_000)))
        {
            edited.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var item = (await ListAsync(user)).GetProperty("items").EnumerateArray().Single();
        item.GetProperty("change").GetString().ShouldBe("new");
        item.GetProperty("previous").ValueKind.ShouldBe(JsonValueKind.Null);
        item.GetProperty("current").GetProperty("amount").GetInt64().ShouldBe(5_000);
        item.GetProperty("suggestedAccountId").ValueKind.ShouldBe(JsonValueKind.Null);

        using (var deleted = await SendAsync(owner, HttpMethod.Delete, $"{GroupsPath}/{trip.Group.Id}/repayments/{repaymentId}"))
        {
            deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await ListAsync(user)).GetProperty("total").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Should_SuggestArchivedAccount_Only_When_ItemHoldsRecordsInIt()
    {
        using var owner = new AuthClient(factory);
        using var user = new AuthClient(factory);
        var trip = await SetUpAsync(owner, user);
        var archived = await TestData.AddAccountAsync(factory, trip.UserId, openingBalance: 50_000, archived: true);
        var active = await TestData.AddAccountAsync(factory, trip.UserId, openingBalance: 50_000);
        var withoutRecords = await CreateExpenseAsync(owner, trip.Group.Id, Paid(trip.Anna, "cafes", "Taxi", 6_000));
        var withRecords = await CreateExpenseAsync(owner, trip.Group.Id, Paid(trip.Anna, "cafes", "Dinner", 15_000));
        var elsewhere = await CreateExpenseAsync(owner, trip.Group.Id, Paid(trip.Anna, "cafes", "Lunch", 9_000));
        var earlier =
            $$"""{"tags": ["dinner"], "description": "Dinner", "share": 4000, "categoryId": "cafes", "date": "{{IsoDay(Today)}}", "currency": "EUR", "amount": 12000}""";
        await SeedConfirmationAsync(trip, GroupEntryKind.Expense, withoutRecords, null, archived.Id, null, null);
        await SeedConfirmationAsync(trip, GroupEntryKind.Expense, withRecords, earlier, archived.Id, null, -8_000);
        await SeedConfirmationAsync(trip, GroupEntryKind.Expense, elsewhere, null, active.Id, null, null);

        var items = (await ListAsync(user)).GetProperty("items").EnumerateArray()
            .ToDictionary(EntryId);

        items[withoutRecords].GetProperty("change").GetString().ShouldBe("new");
        items[withoutRecords].GetProperty("suggestedAccountId").ValueKind.ShouldBe(JsonValueKind.Null);
        items[withRecords].GetProperty("change").GetString().ShouldBe("changed");
        items[withRecords].GetProperty("suggestedAccountId").GetGuid().ShouldBe(archived.Id);
        items[elsewhere].GetProperty("change").GetString().ShouldBe("new");
        items[elsewhere].GetProperty("suggestedAccountId").GetGuid().ShouldBe(active.Id);
    }

    [Fact]
    public async Task Should_ListPaymentAgain_When_OnlyItsManualRateChanges()
    {
        using var owner = new AuthClient(factory);
        using var user = new AuthClient(factory);
        var trip = await SetUpAsync(owner, user);
        var account = await TestData.AddAccountAsync(factory, trip.UserId, openingBalance: 50_000);
        var market = await CreateExpenseAsync(owner, trip.Group.Id, new
        {
            payerId = trip.Olya.Id,
            amount = 9_000,
            currency = "EUR",
            date = IsoDay(Today),
            description = "Market"
        });
        var paid = await CreateRepaymentAsync(owner, trip.Group.Id, PaidInPln(trip, 0.25m));
        var confirmed =
            $$"""{"covers": [{"groupAmount": 3000, "expenseId": "{{market}}"}], "groupAmount": 5000, "date": "{{IsoDay(Today)}}", "amount": 20000, "currency": "PLN", "memberId": "{{trip.Olya.Id}}", "direction": "paid"}""";
        await SeedConfirmationAsync(trip, GroupEntryKind.Repayment, paid, confirmed, account.Id, null, -8_000);

        (await ListAsync(user)).GetProperty("total").GetInt32().ShouldBe(0);

        using (var edited = await SendAsync(
            owner,
            HttpMethod.Put,
            $"{GroupsPath}/{trip.Group.Id}/repayments/{paid}",
            PaidInPln(trip, 0.2m)))
        {
            edited.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var item = (await ListAsync(user)).GetProperty("items").EnumerateArray().Single();
        item.GetProperty("change").GetString().ShouldBe("changed");
        item.GetProperty("previous").GetProperty("groupAmount").GetInt64().ShouldBe(5_000);
        var current = item.GetProperty("current");
        Names(current).ShouldBe(PaidTermProperties);
        current.GetProperty("amount").GetInt64().ShouldBe(20_000);
        current.GetProperty("currency").GetString().ShouldBe("PLN");
        current.GetProperty("groupAmount").GetInt64().ShouldBe(4_000);
        current.GetProperty("covers").EnumerateArray().Single().GetProperty("groupAmount").GetInt64().ShouldBe(3_000);
        item.GetProperty("lines").EnumerateArray().Single().GetProperty("amount").GetInt64().ShouldBe(15_000);
        item.GetProperty("other").GetInt64().ShouldBe(5_000);
        item.GetProperty("suggestedAccountId").GetGuid().ShouldBe(account.Id);
    }

    [Fact]
    public async Task Should_ListRemovals_When_ConfirmedEntriesAreDeleted()
    {
        using var owner = new AuthClient(factory);
        using var user = new AuthClient(factory);
        var trip = await SetUpAsync(owner, user);
        var account = await TestData.AddAccountAsync(factory, trip.UserId, openingBalance: 50_000);
        var expenseId = await CreateExpenseAsync(owner, trip.Group.Id, Paid(trip.Anna, "cafes", "Dinner", 12_000));
        var repaymentId = await CreateRepaymentAsync(owner, trip.Group.Id, Repayment(trip.Olya, trip.Anna, 4_000));
        var expenseTerms =
            $$"""{"amount": 12000, "currency": "EUR", "date": "{{IsoDay(Today)}}", "share": 4000, "categoryId": "cafes", "tags": ["dinner"], "description": "Dinner"}""";
        var repaymentTerms =
            $$"""{"direction": "received", "memberId": "{{trip.Olya.Id}}", "amount": 4000, "currency": "EUR", "date": "{{IsoDay(Today)}}"}""";
        await SeedConfirmationAsync(trip, GroupEntryKind.Expense, expenseId, expenseTerms, account.Id, null, -8_000);
        await SeedConfirmationAsync(trip, GroupEntryKind.Repayment, repaymentId, repaymentTerms, account.Id, null, 4_000);

        (await ListAsync(user)).GetProperty("total").GetInt32().ShouldBe(0);

        foreach (var entry in new[] { $"expenses/{expenseId}", $"repayments/{repaymentId}" })
        {
            using var deleted = await SendAsync(owner, HttpMethod.Delete, $"{GroupsPath}/{trip.Group.Id}/{entry}");
            deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        var items = (await ListAsync(user)).GetProperty("items").EnumerateArray().ToList();
        items.Select(EntryId).ShouldBe([repaymentId, expenseId]);
        items.ShouldAllBe(item => item.GetProperty("change").GetString() == "removed");
        items.ShouldAllBe(item => item.GetProperty("current").ValueKind == JsonValueKind.Null);
        items[0].GetProperty("version").GetString()
            .ShouldBe(VersionOf(await NewestEventAsync(owner, trip.Group.Id, "repayments", repaymentId), trip.Anna));
        Names(items[0].GetProperty("previous")).ShouldBe(ReceivedTermProperties);
        items[1].GetProperty("previous").GetProperty("share").GetInt64().ShouldBe(4_000);
    }

    private async Task<Trip> SetUpAsync(AuthClient owner, AuthClient user)
    {
        await owner.RegisterNewAsync(Ct);
        await user.RegisterNewAsync(Ct);
        var ownerMe = await GetAsync(owner, "/v1/me");
        var userId = await UserIdAsync(user);
        var ownerId = ownerMe.GetProperty("id").GetGuid();
        var (group, members) = await TestData.AddGroupAsync(factory, ownerId);
        await TestData.LinkMemberAsync(factory, members[0], userId);
        return new Trip(
            ownerId,
            ownerMe.GetProperty("name").GetString()!,
            userId,
            group,
            members,
            members[0],
            members[1],
            members[2]);
    }

    private async Task SeedConfirmationAsync(
        Trip trip,
        GroupEntryKind kind,
        Guid entryId,
        string? terms,
        Guid? accountId,
        (Guid TransactionId, Guid ExpenseId)? booked,
        long? leg)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var confirmation = GroupConfirmation.Create(trip.UserId, trip.Group.Id, kind, entryId);
        if (accountId is { } account)
        {
            confirmation.Confirm(terms, account, DateTimeOffset.UtcNow);
        }
        else
        {
            confirmation.Decide(terms, DateTimeOffset.UtcNow);
        }

        db.GroupConfirmations.Add(confirmation);
        if (booked is { } transaction)
        {
            db.GroupConfirmationTransactions.Add(
                GroupConfirmationTransaction.Create(transaction.TransactionId, confirmation.Id, transaction.ExpenseId));
        }

        if (leg is { } amount && accountId is { } legAccount)
        {
            db.GroupAccountLegs.Add(GroupAccountLeg.Create(trip.UserId, confirmation.Id, legAccount, Today, amount));
        }

        await db.SaveChangesAsync(Ct);
    }

    private static object Paid(GroupMember payer, string categoryId, string? description, long amount = 3_000) =>
        new
        {
            payerId = payer.Id,
            amount,
            currency = "EUR",
            date = IsoDay(Today),
            categoryId,
            tags = DinnerTags,
            description
        };

    private static object Repayment(GroupMember from, GroupMember to, long amount) =>
        new { fromMemberId = from.Id, toMemberId = to.Id, amount, currency = "EUR", date = IsoDay(Today) };

    private static object PaidInPln(Trip trip, decimal rate) =>
        new { fromMemberId = trip.Anna.Id, toMemberId = trip.Olya.Id, amount = 20_000, currency = "PLN", rate, date = IsoDay(Today) };

    private static async Task<Guid> CreateExpenseAsync(AuthClient client, Guid groupId, object body)
    {
        using var response = await SendAsync(client, HttpMethod.Post, $"{GroupsPath}/{groupId}/expenses", body);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private static async Task UpdateExpenseAsync(AuthClient client, Guid groupId, Guid expenseId, object body)
    {
        using var response = await SendAsync(client, HttpMethod.Put, $"{GroupsPath}/{groupId}/expenses/{expenseId}", body);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<Guid> CreateRepaymentAsync(AuthClient client, Guid groupId, object body)
    {
        using var response = await SendAsync(client, HttpMethod.Post, $"{GroupsPath}/{groupId}/repayments", body);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private static async Task<string> AddGroupCategoryAsync(AuthClient client, Guid groupId, string name)
    {
        using var response = await SendAsync(client, HttpMethod.Post, $"{GroupsPath}/{groupId}/categories", new { name });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetString()!;
    }

    private static async Task<string> ShareAsync(AuthClient owner, Guid groupId)
    {
        using var response = await SendAsync(owner, HttpMethod.Post, $"{GroupsPath}/{groupId}/share");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("shareToken").GetString()!;
    }

    private static async Task<JsonElement> NewestEventAsync(AuthClient client, Guid groupId, string kind, Guid entryId) =>
        (await GetAsync(client, $"{GroupsPath}/{groupId}/{kind}/{entryId}/history")).GetProperty("items")[0];

    private static string VersionOf(JsonElement newestEvent, GroupMember member) =>
        $"{newestEvent.GetProperty("id").GetGuid()}.{member.Id}";

    private static async Task<Guid> ExpenseCategoryIdAsync(AuthClient client, string name) =>
        (await GetAsync(client, "/v1/categories?kind=expense")).GetProperty("items").EnumerateArray()
            .Single(category => category.GetProperty("name").GetString() == name)
            .GetProperty("id").GetGuid();

    private static Task<JsonElement> ListAsync(AuthClient client, string query = "") =>
        GetAsync(client, ToConfirmPath + query);

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
        response.StatusCode.ShouldBe(HttpStatusCode.OK, path);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task<Guid> UserIdAsync(AuthClient client) =>
        (await GetAsync(client, "/v1/me")).GetProperty("id").GetGuid();

    private static Guid EntryId(JsonElement item) => item.GetProperty("entryId").GetGuid();

    private static string[] Names(JsonElement element) => [.. element.EnumerateObject().Select(property => property.Name)];

    private static string?[] Strings(JsonElement array) => [.. array.EnumerateArray().Select(item => item.GetString())];

    private static string IsoDay(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private sealed record Trip(
        Guid OwnerId,
        string OwnerName,
        Guid UserId,
        Group Group,
        GroupMember[] Members,
        GroupMember Anna,
        GroupMember Olya,
        GroupMember Max);
}
