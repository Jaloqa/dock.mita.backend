using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Accounts;
using Mita.Domain.Categories;
using Mita.Domain.Groups;
using Mita.Domain.Money;
using Mita.Domain.Periods;
using Mita.Domain.Users;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.ToConfirm;

public sealed class ConfirmExpensesTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string ToConfirmPath = "/v1/to-confirm";

    private const string GroupsPath = "/v1/groups";

    private const string ItemChanged = "group.itemChanged";

    private static readonly string[] DinnerTags = ["dinner"];

    private static readonly string[] WorkTags = ["work"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Should_BookOnlyTheShare_AsSpending()
    {
        await using var app = await IsolatedAsync();
        using var owner = new AuthClient(app);
        using var user = new AuthClient(app);
        var trip = await SetUpAsync(app, owner, user);
        var cafes = await ExpenseCategoryIdAsync(user, "Cafes");
        var period = FinancialPeriod.For(Today, MonthStart.Day(1));
        await TestData.AddBudgetAsync(app, trip.UserId, cafes, period.Key.ToString(), 50_000);
        (await BalanceAsync(user, trip.Konto.Id)).ShouldBe(100_000);
        var expenseId = await CreateExpenseAsync(owner, trip.Group.Id, Dinner(trip.Anna, 12_000, Today));

        using (var confirmed = await ConfirmAsync(user, expenseId, Suggested(await ItemAsync(user, expenseId), trip.Konto.Id)))
        {
            confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await BalanceAsync(user, trip.Konto.Id)).ShouldBe(48_400);
        var transaction = (await TransactionsAsync(user)).Single();
        transaction.GetProperty("kind").GetString().ShouldBe("expense");
        transaction.GetProperty("date").GetString().ShouldBe(IsoDay(Today));
        transaction.GetProperty("amount").GetInt64().ShouldBe(4_000);
        transaction.GetProperty("currency").GetString().ShouldBe("EUR");
        transaction.GetProperty("accountId").GetGuid().ShouldBe(trip.Konto.Id);
        transaction.GetProperty("accountAmount").GetInt64().ShouldBe(17_200);
        transaction.GetProperty("rate").GetDouble().ShouldBe(4.3);
        transaction.GetProperty("baseAmount").GetInt64().ShouldBe(17_200);
        transaction.GetProperty("categoryId").GetGuid().ShouldBe(cafes);
        transaction.GetProperty("title").GetString().ShouldBe("Dinner");
        TagNames(transaction).ShouldBe(["dinner"]);
        (await LegsAsync(app, trip.UserId)).ShouldBe([(trip.Konto.Id, Today, -34_400L)]);

        var overview = await GetAsync(user, "/v1/overview");
        overview.GetProperty("expense").GetProperty("amount").GetInt64().ShouldBe(17_200);
        overview.GetProperty("income").GetProperty("amount").GetInt64().ShouldBe(0);
        var budget = (await GetAsync(user, "/v1/budgets")).GetProperty("items").EnumerateArray().Single();
        budget.GetProperty("categoryId").GetGuid().ShouldBe(cafes);
        budget.GetProperty("spent").GetInt64().ShouldBe(17_200);
        var report = await GetAsync(user, "/v1/reports");
        report.GetProperty("totals").GetProperty("income").GetInt64().ShouldBe(0);
        report.GetProperty("totals").GetProperty("expense").GetInt64().ShouldBe(17_200);
        var structure = report.GetProperty("structure").GetProperty("items").EnumerateArray().Single();
        structure.GetProperty("categoryId").GetGuid().ShouldBe(cafes);
        structure.GetProperty("name").GetString().ShouldBe("Cafes");
        structure.GetProperty("amount").GetInt64().ShouldBe(17_200);
        (await ListAsync(user)).GetProperty("total").GetInt32().ShouldBe(0);
        (await GetAsync(user, "/v1/debts")).GetProperty("summary").GetProperty("owedToMe").GetInt64().ShouldBe(34_400);
    }

    [Fact]
    public async Task Should_BookIntoAnyCurrency()
    {
        await using var app = await IsolatedAsync();
        await TestData.AddRatesAsync(app, (Currency.JPY, IsoDay(Today.AddDays(-10)), 0.027m));
        using var owner = new AuthClient(app);
        using var user = new AuthClient(app);
        var trip = await SetUpAsync(app, owner, user);
        var euro = await TestData.AddAccountAsync(app, trip.UserId, Currency.EUR, 50_000);
        var yen = await TestData.AddAccountAsync(app, trip.UserId, Currency.JPY, 100_000);
        var inEuro = await CreateExpenseAsync(owner, trip.Group.Id, Dinner(trip.Anna, 12_000, Today.AddDays(-2)));
        var inYen = await CreateExpenseAsync(owner, trip.Group.Id, Dinner(trip.Anna, 12_000, Today.AddDays(-2)));

        using (var confirmed = await ConfirmAsync(user, inEuro, Suggested(await ItemAsync(user, inEuro), euro.Id)))
        {
            confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        using (var confirmed = await ConfirmAsync(user, inYen, Suggested(await ItemAsync(user, inYen), yen.Id, 160m)))
        {
            confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        var transactions = await TransactionsAsync(user);
        var euroTransaction = transactions.Single(transaction => transaction.GetProperty("accountId").GetGuid() == euro.Id);
        euroTransaction.GetProperty("amount").GetInt64().ShouldBe(4_000);
        euroTransaction.GetProperty("currency").GetString().ShouldBe("EUR");
        euroTransaction.GetProperty("accountAmount").GetInt64().ShouldBe(4_000);
        euroTransaction.GetProperty("rate").GetDouble().ShouldBe(1);
        euroTransaction.GetProperty("baseAmount").GetInt64().ShouldBe(17_200);
        var yenTransaction = transactions.Single(transaction => transaction.GetProperty("accountId").GetGuid() == yen.Id);
        yenTransaction.GetProperty("amount").GetInt64().ShouldBe(4_000);
        yenTransaction.GetProperty("currency").GetString().ShouldBe("EUR");
        yenTransaction.GetProperty("accountAmount").GetInt64().ShouldBe(6_400);
        yenTransaction.GetProperty("rate").GetDouble().ShouldBe(160);
        yenTransaction.GetProperty("baseAmount").GetInt64().ShouldBe(17_280);
        (await LegsAsync(app, trip.UserId)).ShouldBe(
            [(euro.Id, Today.AddDays(-2), -8_000L), (yen.Id, Today.AddDays(-2), -12_800L)],
            ignoreOrder: true);
        (await BalanceAsync(user, euro.Id)).ShouldBe(38_000);
        (await BalanceAsync(user, yen.Id)).ShouldBe(80_800);
    }

    [Fact]
    public async Task Should_CreateCategory_When_Requested()
    {
        await using var app = await IsolatedAsync();
        using var owner = new AuthClient(app);
        using var user = new AuthClient(app);
        var trip = await SetUpAsync(app, owner, user);
        var categoriesBefore = (await ExpenseCategoriesAsync(user)).Count;
        var trips = await CreateExpenseAsync(owner, trip.Group.Id, Dinner(trip.Anna, 12_000, Today.AddDays(-2)));
        var lunch = await CreateExpenseAsync(owner, trip.Group.Id, Dinner(trip.Anna, 6_000, Today.AddDays(-2)));

        var tripsItem = await ItemAsync(user, trips);
        using (var confirmed = await ConfirmAsync(
            user,
            trips,
            Body(tripsItem, trip.Konto.Id, new { newCategory = "  Trips ", title = "Ferry", tags = DinnerTags })))
        {
            confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        var lunchItem = await ItemAsync(user, lunch);
        using (var confirmed = await ConfirmAsync(
            user,
            lunch,
            Body(lunchItem, trip.Konto.Id, new { categoryId = (Guid?)null, newCategory = "cafes", title = "Lunch" })))
        {
            confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        var categories = await ExpenseCategoriesAsync(user);
        categories.Count.ShouldBe(categoriesBefore + 1);
        var created = categories.Single(category => category.GetProperty("name").GetString() == "Trips");
        created.GetProperty("kind").GetString().ShouldBe("expense");
        created.GetProperty("color").GetString().ShouldBe("chart-4");
        var transactions = await TransactionsAsync(user);
        transactions.Single(transaction => transaction.GetProperty("title").GetString() == "Ferry")
            .GetProperty("categoryId").GetGuid().ShouldBe(created.GetProperty("id").GetGuid());
        transactions.Single(transaction => transaction.GetProperty("title").GetString() == "Lunch")
            .GetProperty("categoryId").GetGuid().ShouldBe(await ExpenseCategoryIdAsync(user, "Cafes"));
    }

    [Fact]
    public async Task Should_KeepRecords_UntilChangeConfirmed()
    {
        await using var app = await IsolatedAsync();
        using var owner = new AuthClient(app);
        using var user = new AuthClient(app);
        var trip = await SetUpAsync(app, owner, user);
        var leisure = await ExpenseCategoryIdAsync(user, "Leisure");
        var date = Today.AddDays(-2);
        var expenseId = await CreateExpenseAsync(owner, trip.Group.Id, Dinner(trip.Anna, 12_000, date));
        var first = await ItemAsync(user, expenseId);
        using (var confirmed = await ConfirmAsync(
            user,
            expenseId,
            Body(first, trip.Konto.Id, new { categoryId = leisure, title = "Team dinner", tags = WorkTags })))
        {
            confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        await UpdateExpenseAsync(owner, trip.Group.Id, expenseId, Dinner(trip.Anna, 15_000, date));

        (await BookedAsync(user)).ShouldBe([(4_000L, 17_200L)]);
        (await BalanceAsync(user, trip.Konto.Id)).ShouldBe(48_400);
        var changed = await ItemAsync(user, expenseId);
        changed.GetProperty("change").GetString().ShouldBe("changed");
        changed.GetProperty("previous").GetProperty("amount").GetInt64().ShouldBe(12_000);
        changed.GetProperty("previous").GetProperty("share").GetInt64().ShouldBe(4_000);
        changed.GetProperty("current").GetProperty("amount").GetInt64().ShouldBe(15_000);
        changed.GetProperty("current").GetProperty("share").GetInt64().ShouldBe(5_000);
        changed.GetProperty("suggestedAccountId").GetGuid().ShouldBe(trip.Konto.Id);
        var suggestion = changed.GetProperty("lines").EnumerateArray().Single().GetProperty("suggestion");
        suggestion.GetProperty("categoryId").GetGuid().ShouldBe(leisure);
        suggestion.GetProperty("title").GetString().ShouldBe("Team dinner");
        Strings(suggestion.GetProperty("tags")).ShouldBe(["work"]);

        using (var confirmed = await ConfirmAsync(user, expenseId, Suggested(changed, trip.Konto.Id)))
        {
            confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await BookedAsync(user)).ShouldBe([(5_000L, 21_500L)]);
        var rebooked = (await TransactionsAsync(user)).Single();
        rebooked.GetProperty("categoryId").GetGuid().ShouldBe(leisure);
        rebooked.GetProperty("title").GetString().ShouldBe("Team dinner");
        TagNames(rebooked).ShouldBe(["work"]);
        (await BalanceAsync(user, trip.Konto.Id)).ShouldBe(35_500);
        (await LegsAsync(app, trip.UserId)).ShouldBe([(trip.Konto.Id, date, -43_000L)]);

        await SendOkAsync(owner, HttpMethod.Delete, ExpensePath(trip, expenseId), HttpStatusCode.NoContent);

        var removed = await ItemAsync(user, expenseId);
        removed.GetProperty("change").GetString().ShouldBe("removed");
        removed.GetProperty("current").ValueKind.ShouldBe(JsonValueKind.Null);
        removed.GetProperty("lines").GetArrayLength().ShouldBe(0);

        using (var rejected = await RejectAsync(user, expenseId, Version(removed)))
        {
            rejected.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await BookedAsync(user)).ShouldBe([(5_000L, 21_500L)]);
        (await BalanceAsync(user, trip.Konto.Id)).ShouldBe(35_500);
        (await FindItemAsync(user, expenseId)).ShouldBeNull();

        await SendOkAsync(owner, HttpMethod.Post, $"{ExpensePath(trip, expenseId)}/restore", HttpStatusCode.OK);
        (await FindItemAsync(user, expenseId)).ShouldBeNull();

        await SendOkAsync(owner, HttpMethod.Delete, ExpensePath(trip, expenseId), HttpStatusCode.NoContent);
        (await FindItemAsync(user, expenseId)).ShouldBeNull();

        await SendOkAsync(owner, HttpMethod.Post, $"{ExpensePath(trip, expenseId)}/restore", HttpStatusCode.OK);
        await UpdateExpenseAsync(owner, trip.Group.Id, expenseId, Dinner(trip.Anna, 9_000, date));
        var again = await ItemAsync(user, expenseId);
        again.GetProperty("change").GetString().ShouldBe("changed");
        again.GetProperty("previous").GetProperty("amount").GetInt64().ShouldBe(15_000);

        using (var confirmed = await ConfirmAsync(user, expenseId, Suggested(again, trip.Konto.Id)))
        {
            confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await BookedAsync(user)).ShouldBe([(3_000L, 12_900L)]);
        (await BalanceAsync(user, trip.Konto.Id)).ShouldBe(61_300);
        (await LegsAsync(app, trip.UserId)).ShouldBe([(trip.Konto.Id, date, -25_800L)]);
        (await ListAsync(user)).GetProperty("total").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Should_RemoveRecords_When_RemovalConfirmed()
    {
        await using var app = await IsolatedAsync();
        using var owner = new AuthClient(app);
        using var user = new AuthClient(app);
        var trip = await SetUpAsync(app, owner, user);
        var expenseId = await CreateExpenseAsync(owner, trip.Group.Id, Dinner(trip.Anna, 12_000, Today.AddDays(-2)));
        using (var confirmed = await ConfirmAsync(user, expenseId, Suggested(await ItemAsync(user, expenseId), trip.Konto.Id)))
        {
            confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await BalanceAsync(user, trip.Konto.Id)).ShouldBe(48_400);
        await SendOkAsync(owner, HttpMethod.Delete, ExpensePath(trip, expenseId), HttpStatusCode.NoContent);
        var removed = await ItemAsync(user, expenseId);
        removed.GetProperty("change").GetString().ShouldBe("removed");

        using (var confirmed = await ConfirmAsync(user, expenseId, new { version = Version(removed) }))
        {
            confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await TransactionsAsync(user)).ShouldBeEmpty();
        (await LegsAsync(app, trip.UserId)).ShouldBeEmpty();
        (await BalanceAsync(user, trip.Konto.Id)).ShouldBe(100_000);
        (await ListAsync(user)).GetProperty("total").GetInt32().ShouldBe(0);

        await SendOkAsync(owner, HttpMethod.Post, $"{ExpensePath(trip, expenseId)}/restore", HttpStatusCode.OK);

        var restored = await ItemAsync(user, expenseId);
        restored.GetProperty("change").GetString().ShouldBe("new");
        restored.GetProperty("previous").ValueKind.ShouldBe(JsonValueKind.Null);
        restored.GetProperty("suggestedAccountId").GetGuid().ShouldBe(trip.Konto.Id);
    }

    [Fact]
    public async Task Should_NotSuggestArchivedAccount_When_ItemHoldsNoRecords()
    {
        await using var app = await IsolatedAsync();
        using var owner = new AuthClient(app);
        using var user = new AuthClient(app);
        var trip = await SetUpAsync(app, owner, user);
        var expenseId = await CreateExpenseAsync(owner, trip.Group.Id, Dinner(trip.Anna, 12_000, Today.AddDays(-2)));
        using (var confirmed = await ConfirmAsync(user, expenseId, Suggested(await ItemAsync(user, expenseId), trip.Konto.Id)))
        {
            confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        await SendOkAsync(owner, HttpMethod.Delete, ExpensePath(trip, expenseId), HttpStatusCode.NoContent);
        using (var confirmed = await ConfirmAsync(user, expenseId, new { version = Version(await ItemAsync(user, expenseId)) }))
        {
            confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        await SendOkAsync(
            user,
            HttpMethod.Put,
            $"/v1/accounts/{trip.Konto.Id}",
            HttpStatusCode.OK,
            new { name = trip.Konto.Name, type = "card", currency = "PLN", openingBalance = 100_000, archived = true });
        await SendOkAsync(owner, HttpMethod.Post, $"{ExpensePath(trip, expenseId)}/restore", HttpStatusCode.OK);

        var restored = await ItemAsync(user, expenseId);

        restored.GetProperty("change").GetString().ShouldBe("new");
        restored.GetProperty("suggestedAccountId").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Should_KeepRecords_When_GroupDeletedOrUnlinked()
    {
        await using var app = await IsolatedAsync();
        using var owner = new AuthClient(app);
        using var user = new AuthClient(app);
        var trip = await SetUpAsync(app, owner, user);
        var dinner = await CreateExpenseAsync(owner, trip.Group.Id, Dinner(trip.Anna, 12_000, Today.AddDays(-2)));
        using (var confirmed = await ConfirmAsync(user, dinner, Suggested(await ItemAsync(user, dinner), trip.Konto.Id)))
        {
            confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        await SendOkAsync(owner, HttpMethod.Delete, $"{GroupsPath}/{trip.Group.Id}", HttpStatusCode.NoContent);

        (await BookedAsync(user)).ShouldBe([(4_000L, 17_200L)]);
        (await BalanceAsync(user, trip.Konto.Id)).ShouldBe(48_400);
        (await ListAsync(user)).GetProperty("total").GetInt32().ShouldBe(0);

        var (home, members) = await TestData.AddGroupAsync(app, trip.OwnerId, "Home", Currency.EUR, "Ula", "Vera");
        await TestData.LinkMemberAsync(app, members[0], trip.UserId);
        var lunch = await CreateExpenseAsync(owner, home.Id, Dinner(members[0], 6_000, Today.AddDays(-2)));
        using (var confirmed = await ConfirmAsync(user, lunch, Suggested(await ItemAsync(user, lunch), trip.Konto.Id)))
        {
            confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await BalanceAsync(user, trip.Konto.Id)).ShouldBe(22_600);

        await SendOkAsync(user, HttpMethod.Delete, $"{GroupsPath}/{home.Id}/membership", HttpStatusCode.NoContent);

        (await BookedAsync(user)).ShouldBe([(3_000L, 12_900L), (4_000L, 17_200L)], ignoreOrder: true);
        (await BalanceAsync(user, trip.Konto.Id)).ShouldBe(22_600);
        (await LegsAsync(app, trip.UserId)).Select(leg => leg.AccountAmount).ShouldBe([-34_400L, -12_900L], ignoreOrder: true);
        (await ListAsync(user)).GetProperty("total").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Should_Return409_When_ItemChangedMeanwhile()
    {
        await using var app = await IsolatedAsync();
        using var owner = new AuthClient(app);
        using var user = new AuthClient(app);
        var trip = await SetUpAsync(app, owner, user);
        var expenseId = await CreateExpenseAsync(owner, trip.Group.Id, Dinner(trip.Anna, 12_000, Today.AddDays(-2)));
        var stale = await ItemAsync(user, expenseId);

        await UpdateExpenseAsync(owner, trip.Group.Id, expenseId, Dinner(trip.Anna, 15_000, Today.AddDays(-2)));

        using (var confirmed = await ConfirmAsync(user, expenseId, Suggested(stale, trip.Konto.Id)))
        {
            await ShouldHaveCodeAsync(confirmed, HttpStatusCode.Conflict, ItemChanged);
        }

        using (var rejected = await RejectAsync(user, expenseId, Version(stale)))
        {
            await ShouldHaveCodeAsync(rejected, HttpStatusCode.Conflict, ItemChanged);
        }

        (await TransactionsAsync(user)).ShouldBeEmpty();
        (await LegsAsync(app, trip.UserId)).ShouldBeEmpty();
        (await BalanceAsync(user, trip.Konto.Id)).ShouldBe(100_000);
        (await ConfirmationCountAsync(app, trip.UserId)).ShouldBe(0);
        var current = await ItemAsync(user, expenseId);
        current.GetProperty("change").GetString().ShouldBe("new");
        Version(current).ShouldNotBe(Version(stale));
        current.GetProperty("current").GetProperty("amount").GetInt64().ShouldBe(15_000);
    }

    [Fact]
    public async Task Should_BookOnce_When_ConfirmedTwiceAtOnce()
    {
        await using var app = await IsolatedAsync();
        using var owner = new AuthClient(app);
        using var user = new AuthClient(app);
        var trip = await SetUpAsync(app, owner, user);

        for (var round = 1; round <= 3; round++)
        {
            var expenseId = await CreateExpenseAsync(owner, trip.Group.Id, Dinner(trip.Anna, 12_000, Today.AddDays(-2)));
            var body = Suggested(await ItemAsync(user, expenseId), trip.Konto.Id);

            var responses = await Task.WhenAll(ConfirmAsync(user, expenseId, body), ConfirmAsync(user, expenseId, body));
            try
            {
                responses.Count(response => response.StatusCode == HttpStatusCode.NoContent).ShouldBe(1);
                responses.Single(response => response.StatusCode != HttpStatusCode.NoContent).StatusCode
                    .ShouldBeOneOf(HttpStatusCode.Conflict, HttpStatusCode.NotFound);
            }
            finally
            {
                foreach (var response in responses)
                {
                    response.Dispose();
                }
            }

            (await TransactionsAsync(user)).Count.ShouldBe(round);
            (await LegsAsync(app, trip.UserId)).Count.ShouldBe(round);
            (await BalanceAsync(user, trip.Konto.Id)).ShouldBe(100_000 - (round * 51_600));
        }
    }

    [Fact]
    public async Task Should_Return404_ForItemsOfOthers()
    {
        await using var app = await IsolatedAsync();
        using var owner = new AuthClient(app);
        using var user = new AuthClient(app);
        var trip = await SetUpAsync(app, owner, user);
        var own = await CreateExpenseAsync(owner, trip.Group.Id, Dinner(trip.Anna, 12_000, Today.AddDays(-2)));
        var olyaPaid = await CreateExpenseAsync(owner, trip.Group.Id, Dinner(trip.Olya, 9_000, Today.AddDays(-2)));
        var (elsewhere, elsewhereMembers) = await TestData.AddGroupAsync(app, trip.OwnerId, "Elsewhere");
        var notLinked = await TestData.AddGroupExpenseAsync(app, elsewhere, elsewhereMembers[0], 9_000, elsewhereMembers);
        var item = await ItemAsync(user, own);
        var body = Suggested(item, trip.Konto.Id);

        foreach (var (kind, entryId) in new[]
                 {
                     ("expenses", olyaPaid), ("expenses", notLinked.Id), ("expenses", Guid.NewGuid()), ("repayments", own),
                     ("transfers", own), ("expense", own)
                 })
        {
            using var confirmed = await ConfirmAsync(user, entryId, body, kind);
            confirmed.StatusCode.ShouldBe(HttpStatusCode.NotFound, $"{kind}/{entryId}");
            using var rejected = await RejectAsync(user, entryId, Version(item), kind);
            rejected.StatusCode.ShouldBe(HttpStatusCode.NotFound, $"{kind}/{entryId}");
        }

        using (var ownerConfirmed = await ConfirmAsync(owner, own, body))
        {
            ownerConfirmed.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        using (var ownerRejected = await RejectAsync(owner, own, Version(item)))
        {
            ownerRejected.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        using (var shapeFirst = await ConfirmAsync(user, Guid.NewGuid(), new { accountId = trip.Konto.Id }))
        {
            await ShouldHaveErrorsAsync(shapeFirst, new() { ["version"] = ["validation.required"] });
        }

        using var anonymous = app.CreateClient();
        using (var confirmed = await anonymous.PostAsJsonAsync($"{ToConfirmPath}/expenses/{own}/confirm", body, Ct))
        {
            confirmed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using (var rejected = await anonymous.PostAsJsonAsync($"{ToConfirmPath}/expenses/{own}/reject", new { version = Version(item) }, Ct))
        {
            rejected.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        (await TransactionsAsync(user)).ShouldBeEmpty();
        (await ConfirmationCountAsync(app, trip.UserId)).ShouldBe(0);
        (await ItemAsync(user, own)).GetProperty("change").GetString().ShouldBe("new");
    }

    [Fact]
    public async Task Should_RefuseReferences()
    {
        await using var app = await IsolatedAsync();
        using var owner = new AuthClient(app);
        using var user = new AuthClient(app);
        using var stranger = new AuthClient(app);
        var trip = await SetUpAsync(app, owner, user);
        await stranger.RegisterNewAsync(Ct);
        var strangerAccount = await TestData.AddAccountAsync(app, await UserIdAsync(stranger), openingBalance: 100_000);
        var archived = await TestData.AddAccountAsync(app, trip.UserId, openingBalance: 100_000, archived: true);
        var transfer = await TransferCategoryIdAsync(app, trip.UserId);
        var salary = (await GetAsync(user, "/v1/categories?kind=income")).GetProperty("items").EnumerateArray()
            .Single(category => category.GetProperty("name").GetString() == "Salary").GetProperty("id").GetGuid();
        var cafes = await ExpenseCategoryIdAsync(user, "Cafes");
        var expenseId = await CreateExpenseAsync(owner, trip.Group.Id, Dinner(trip.Anna, 12_000, Today.AddDays(-2)));
        var item = await ItemAsync(user, expenseId);
        var version = Version(item);

        var cases = new (object Body, Dictionary<string, string[]> Errors)[]
        {
            (
                new { version, accountId = strangerAccount.Id, lines = new[] { new { categoryId = transfer } } },
                new() { ["accountId"] = ["validation.unknownAccount"], ["lines.0.categoryId"] = ["validation.unknownCategory"] }
            ),
            (
                new { version, accountId = archived.Id, lines = new[] { new { categoryId = salary } } },
                new() { ["accountId"] = ["validation.archivedAccount"], ["lines.0.categoryId"] = ["validation.unknownCategory"] }
            ),
            (
                new { version, accountId = Guid.NewGuid(), lines = new[] { new { categoryId = Guid.NewGuid() } } },
                new() { ["accountId"] = ["validation.unknownAccount"], ["lines.0.categoryId"] = ["validation.unknownCategory"] }
            ),
            (
                new { version, accountId = trip.Konto.Id, lines = new[] { new { newCategory = "transfers" } } },
                new() { ["lines.0.newCategory"] = ["validation.categoryNameTaken"] }
            ),
            (
                new { version, lines = new[] { new { categoryId = cafes } } },
                new() { ["accountId"] = ["validation.required"] }
            ),
            (
                new { version, accountId = trip.Konto.Id },
                new() { ["lines"] = ["validation.invalidValue"] }
            ),
            (
                new { version, accountId = trip.Konto.Id, lines = new[] { new { categoryId = cafes }, new { categoryId = cafes } } },
                new() { ["lines"] = ["validation.invalidValue"] }
            ),
            (
                new { version, accountId = archived.Id, lines = Array.Empty<object>() },
                new() { ["accountId"] = ["validation.archivedAccount"], ["lines"] = ["validation.invalidValue"] }
            )
        };

        foreach (var (body, errors) in cases)
        {
            using var response = await ConfirmAsync(user, expenseId, body);
            await ShouldHaveErrorsAsync(response, errors);
        }

        (await TransactionsAsync(user)).ShouldBeEmpty();
        (await LegsAsync(app, trip.UserId)).ShouldBeEmpty();
        (await ConfirmationCountAsync(app, trip.UserId)).ShouldBe(0);
        (await ExpenseCategoriesAsync(user)).ShouldNotContain(category => category.GetProperty("name").GetString() == "transfers");
        (await ItemAsync(user, expenseId)).GetProperty("change").GetString().ShouldBe("new");
    }

    [Fact]
    public async Task Should_RebookIntoArchivedAccount_When_ItHoldsTheRecords()
    {
        await using var app = await IsolatedAsync();
        using var owner = new AuthClient(app);
        using var user = new AuthClient(app);
        var trip = await SetUpAsync(app, owner, user);
        var archived = await TestData.AddAccountAsync(app, trip.UserId, openingBalance: 100_000, archived: true);
        var expenseId = await CreateExpenseAsync(owner, trip.Group.Id, Dinner(trip.Anna, 12_000, Today.AddDays(-2)));
        using (var confirmed = await ConfirmAsync(user, expenseId, Suggested(await ItemAsync(user, expenseId), trip.Konto.Id)))
        {
            confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        await SendOkAsync(
            user,
            HttpMethod.Put,
            $"/v1/accounts/{trip.Konto.Id}",
            HttpStatusCode.OK,
            new { name = trip.Konto.Name, type = "card", currency = "PLN", openingBalance = 100_000, archived = true });
        await UpdateExpenseAsync(owner, trip.Group.Id, expenseId, Dinner(trip.Anna, 15_000, Today.AddDays(-2)));
        var changed = await ItemAsync(user, expenseId);
        changed.GetProperty("suggestedAccountId").GetGuid().ShouldBe(trip.Konto.Id);

        using (var elsewhere = await ConfirmAsync(user, expenseId, Suggested(changed, archived.Id)))
        {
            await ShouldHaveErrorsAsync(elsewhere, new() { ["accountId"] = ["validation.archivedAccount"] });
        }

        using (var confirmed = await ConfirmAsync(user, expenseId, Suggested(changed, trip.Konto.Id)))
        {
            confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await BookedAsync(user)).ShouldBe([(5_000L, 21_500L)]);
        (await BalanceAsync(user, trip.Konto.Id)).ShouldBe(35_500);
    }

    [Fact]
    public async Task Should_Return503_When_RateIsMissing()
    {
        await using var app = await IsolatedAsync();
        using var owner = new AuthClient(app);
        using var user = new AuthClient(app);
        var trip = await SetUpAsync(app, owner, user);
        var (kuwait, members) = await TestData.AddGroupAsync(app, trip.OwnerId, "Kuwait", Currency.KWD, "Ali", "Bea", "Cem");
        await TestData.LinkMemberAsync(app, members[0], trip.UserId);
        var expenseId = await CreateExpenseAsync(owner, kuwait.Id, new
        {
            payerId = members[0].Id,
            amount = 6_000,
            currency = "KWD",
            date = IsoDay(Today.AddDays(-2)),
            categoryId = "cafes",
            description = "Dinner"
        });
        var item = await ItemAsync(user, expenseId);

        using (var unavailable = await ConfirmAsync(user, expenseId, Suggested(item, trip.Konto.Id)))
        {
            await ShouldHaveCodeAsync(unavailable, HttpStatusCode.ServiceUnavailable, "rates.unavailable");
        }

        (await TransactionsAsync(user)).ShouldBeEmpty();
        (await LegsAsync(app, trip.UserId)).ShouldBeEmpty();
        (await ConfirmationCountAsync(app, trip.UserId)).ShouldBe(0);

        using (var confirmed = await ConfirmAsync(user, expenseId, Suggested(item, trip.Konto.Id, 13.5m)))
        {
            confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        var transaction = (await TransactionsAsync(user)).Single();
        transaction.GetProperty("amount").GetInt64().ShouldBe(2_000);
        transaction.GetProperty("currency").GetString().ShouldBe("KWD");
        transaction.GetProperty("accountAmount").GetInt64().ShouldBe(2_700);
        transaction.GetProperty("rate").GetDouble().ShouldBe(13.5);
        transaction.GetProperty("baseAmount").GetInt64().ShouldBe(2_700);
        (await LegsAsync(app, trip.UserId)).ShouldBe([(trip.Konto.Id, Today.AddDays(-2), -5_400L)]);
        (await BalanceAsync(user, trip.Konto.Id)).ShouldBe(91_900);
    }

    [Fact]
    public async Task Should_RejectWithoutBooking()
    {
        await using var app = await IsolatedAsync();
        using var owner = new AuthClient(app);
        using var user = new AuthClient(app);
        var trip = await SetUpAsync(app, owner, user);
        (await BalanceAsync(user, trip.Konto.Id)).ShouldBe(100_000);
        var expenseId = await CreateExpenseAsync(owner, trip.Group.Id, Dinner(trip.Anna, 12_000, Today.AddDays(-2)));

        using (var rejected = await RejectAsync(user, expenseId, Version(await ItemAsync(user, expenseId))))
        {
            rejected.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await TransactionsAsync(user)).ShouldBeEmpty();
        (await LegsAsync(app, trip.UserId)).ShouldBeEmpty();
        (await BalanceAsync(user, trip.Konto.Id)).ShouldBe(100_000);
        (await ListAsync(user)).GetProperty("total").GetInt32().ShouldBe(0);

        await UpdateExpenseAsync(owner, trip.Group.Id, expenseId, Dinner(trip.Anna, 15_000, Today.AddDays(-2)));

        var back = await ItemAsync(user, expenseId);
        back.GetProperty("change").GetString().ShouldBe("new");
        back.GetProperty("previous").ValueKind.ShouldBe(JsonValueKind.Null);
        back.GetProperty("suggestedAccountId").ValueKind.ShouldBe(JsonValueKind.Null);
        back.GetProperty("current").GetProperty("amount").GetInt64().ShouldBe(15_000);
    }

    private async Task<WebApplicationFactory<Program>> IsolatedAsync()
    {
        var app = factory.WithIsolatedData();
        await TestData.AddRatesAsync(app, (Currency.EUR, IsoDay(Today.AddDays(-10)), 4.30m));
        return app;
    }

    private static async Task<Trip> SetUpAsync(WebApplicationFactory<Program> app, AuthClient owner, AuthClient user)
    {
        await owner.RegisterNewAsync(Ct);
        await user.RegisterNewAsync(Ct);
        var ownerId = await UserIdAsync(owner);
        var userId = await UserIdAsync(user);
        var (group, members) = await TestData.AddGroupAsync(app, ownerId);
        await TestData.LinkMemberAsync(app, members[0], userId);
        var konto = await TestData.AddAccountAsync(app, userId, openingBalance: 100_000);
        return new Trip(ownerId, userId, group, members[0], members[1], konto);
    }

    private static object Dinner(GroupMember payer, long amount, DateOnly date) =>
        new
        {
            payerId = payer.Id,
            amount,
            currency = "EUR",
            date = IsoDay(date),
            categoryId = "cafes",
            tags = DinnerTags,
            description = "Dinner"
        };

    private static object Suggested(JsonElement item, Guid accountId, decimal? rate = null) =>
        new
        {
            version = Version(item),
            accountId,
            rate,
            lines = item.GetProperty("lines").EnumerateArray()
                .Select(line => line.GetProperty("suggestion"))
                .Select(suggestion => new
                {
                    categoryId = suggestion.GetProperty("categoryId").ValueKind == JsonValueKind.Null
                        ? (Guid?)null
                        : suggestion.GetProperty("categoryId").GetGuid(),
                    newCategory = suggestion.GetProperty("newCategory").GetString(),
                    title = suggestion.GetProperty("title").GetString(),
                    tags = Strings(suggestion.GetProperty("tags"))
                })
                .ToArray()
        };

    private static object Body(JsonElement item, Guid accountId, params object[] lines) =>
        new { version = Version(item), accountId, lines };

    private static Guid Version(JsonElement item) => item.GetProperty("version").GetGuid();

    private static string ExpensePath(Trip trip, Guid expenseId) => $"{GroupsPath}/{trip.Group.Id}/expenses/{expenseId}";

    private static Task<HttpResponseMessage> ConfirmAsync(AuthClient client, Guid entryId, object body, string kind = "expenses") =>
        SendAsync(client, HttpMethod.Post, $"{ToConfirmPath}/{kind}/{entryId}/confirm", body);

    private static Task<HttpResponseMessage> RejectAsync(AuthClient client, Guid entryId, Guid version, string kind = "expenses") =>
        SendAsync(client, HttpMethod.Post, $"{ToConfirmPath}/{kind}/{entryId}/reject", new { version });

    private static async Task<Guid> CreateExpenseAsync(AuthClient client, Guid groupId, object body)
    {
        using var response = await SendAsync(client, HttpMethod.Post, $"{GroupsPath}/{groupId}/expenses", body);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private static Task UpdateExpenseAsync(AuthClient client, Guid groupId, Guid expenseId, object body) =>
        SendOkAsync(client, HttpMethod.Put, $"{GroupsPath}/{groupId}/expenses/{expenseId}", HttpStatusCode.OK, body);

    private static async Task SendOkAsync(
        AuthClient client,
        HttpMethod method,
        string path,
        HttpStatusCode expected,
        object? body = null)
    {
        using var response = await SendAsync(client, method, path, body);
        response.StatusCode.ShouldBe(expected, path);
    }

    private static async Task<JsonElement> ItemAsync(AuthClient client, Guid entryId) =>
        await FindItemAsync(client, entryId) ?? throw new ShouldAssertException($"The item {entryId} is not listed.");

    private static async Task<JsonElement?> FindItemAsync(AuthClient client, Guid entryId)
    {
        foreach (var item in (await ListAsync(client)).GetProperty("items").EnumerateArray())
        {
            if (item.GetProperty("entryId").GetGuid() == entryId)
            {
                return item;
            }
        }

        return null;
    }

    private static Task<JsonElement> ListAsync(AuthClient client) => GetAsync(client, ToConfirmPath);

    private static async Task<List<JsonElement>> TransactionsAsync(AuthClient client) =>
        [.. (await GetAsync(client, "/v1/transactions?pageSize=100")).GetProperty("items").EnumerateArray()];

    private static async Task<List<(long Amount, long AccountAmount)>> BookedAsync(AuthClient client) =>
        [
            .. (await TransactionsAsync(client)).Select(transaction => (
                transaction.GetProperty("amount").GetInt64(),
                transaction.GetProperty("accountAmount").GetInt64()))
        ];

    private static async Task<long> BalanceAsync(AuthClient client, Guid accountId) =>
        (await GetAsync(client, "/v1/accounts")).GetProperty("items").EnumerateArray()
            .Single(account => account.GetProperty("id").GetGuid() == accountId)
            .GetProperty("balance").GetInt64();

    private static async Task<List<JsonElement>> ExpenseCategoriesAsync(AuthClient client) =>
        [.. (await GetAsync(client, "/v1/categories?kind=expense")).GetProperty("items").EnumerateArray()];

    private static async Task<Guid> ExpenseCategoryIdAsync(AuthClient client, string name) =>
        (await ExpenseCategoriesAsync(client))
            .Single(category => category.GetProperty("name").GetString() == name)
            .GetProperty("id").GetGuid();

    private static async Task<Guid> TransferCategoryIdAsync(WebApplicationFactory<Program> app, Guid userId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Categories
            .Where(category => category.UserId == userId && category.System == SystemCategory.Transfer)
            .Select(category => category.Id)
            .SingleAsync(Ct);
    }

    private static async Task<List<(Guid AccountId, DateOnly Date, long AccountAmount)>> LegsAsync(
        WebApplicationFactory<Program> app,
        Guid userId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var legs = await db.GroupAccountLegs.AsNoTracking()
            .Where(leg => leg.UserId == userId)
            .Select(leg => new { leg.AccountId, leg.Date, leg.AccountAmount })
            .ToListAsync(Ct);
        return [.. legs.Select(leg => (leg.AccountId, leg.Date, leg.AccountAmount))];
    }

    private static async Task<int> ConfirmationCountAsync(WebApplicationFactory<Program> app, Guid userId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.GroupConfirmations.CountAsync(confirmation => confirmation.UserId == userId, Ct);
    }

    private static async Task ShouldHaveErrorsAsync(HttpResponseMessage response, Dictionary<string, string[]> errors)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var actual = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        actual.EnumerateObject().Select(property => property.Name).ShouldBe(errors.Keys, ignoreOrder: true);
        foreach (var (field, codes) in errors)
        {
            Strings(actual.GetProperty(field)).ShouldBe(codes, field);
        }
    }

    private static async Task ShouldHaveCodeAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.ShouldBe(status);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString().ShouldBe(code);
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
        response.StatusCode.ShouldBe(HttpStatusCode.OK, path);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task<Guid> UserIdAsync(AuthClient client) =>
        (await GetAsync(client, "/v1/me")).GetProperty("id").GetGuid();

    private static string?[] TagNames(JsonElement transaction) =>
        [.. transaction.GetProperty("tags").EnumerateArray().Select(tag => tag.GetProperty("name").GetString())];

    private static string?[] Strings(JsonElement array) => [.. array.EnumerateArray().Select(item => item.GetString())];

    private static string IsoDay(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private sealed record Trip(Guid OwnerId, Guid UserId, Group Group, GroupMember Anna, GroupMember Olya, Account Konto);
}
