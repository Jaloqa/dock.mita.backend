using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Accounts;
using Mita.Domain.Groups;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.ToConfirm;

public sealed class ConfirmRepaymentsTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string ToConfirmPath = "/v1/to-confirm";

    private const string GroupsPath = "/v1/groups";

    private const string ItemChanged = "group.itemChanged";

    private static readonly string[] ReceivedTermProperties = ["direction", "memberId", "amount", "currency", "date"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static DateOnly PaidOn => Today.AddDays(-1);

    [Fact]
    public async Task Should_BookCoveredShares_OnThePaymentDate()
    {
        await using var app = await IsolatedAsync();
        using var owner = new AuthClient(app);
        using var user = new AuthClient(app);
        var trip = await SetUpAsync(app, owner, user);
        var groceries = await ExpenseCategoryIdAsync(user, "Groceries");
        var repaymentId = await CreateRepaymentAsync(owner, trip, Repayment(trip.Anna, trip.Olya, 3_000, PaidOn, trip.Market));

        var item = await ItemAsync(user, repaymentId);

        item.GetProperty("kind").GetString().ShouldBe("repayment");
        item.GetProperty("change").GetString().ShouldBe("new");
        item.GetProperty("other").GetInt64().ShouldBe(0);
        var current = item.GetProperty("current");
        current.GetProperty("direction").GetString().ShouldBe("paid");
        current.GetProperty("memberId").GetGuid().ShouldBe(trip.Olya.Id);
        current.GetProperty("groupAmount").GetInt64().ShouldBe(3_000);
        Covers(current).ShouldBe([(trip.Market, 3_000L)]);
        var line = item.GetProperty("lines").EnumerateArray().Single();
        line.GetProperty("expenseId").GetGuid().ShouldBe(trip.Market);
        line.GetProperty("description").GetString().ShouldBe("Market");
        line.GetProperty("amount").GetInt64().ShouldBe(3_000);
        line.GetProperty("currency").GetString().ShouldBe("EUR");
        var suggestion = line.GetProperty("suggestion");
        suggestion.GetProperty("categoryId").GetGuid().ShouldBe(groceries);
        suggestion.GetProperty("newCategory").ValueKind.ShouldBe(JsonValueKind.Null);
        suggestion.GetProperty("title").GetString().ShouldBe("Market");

        using (var confirmed = await ConfirmAsync(user, repaymentId, Suggested(item, trip.Konto.Id)))
        {
            confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        var transaction = (await TransactionsAsync(user)).Single();
        transaction.GetProperty("kind").GetString().ShouldBe("expense");
        transaction.GetProperty("date").GetString().ShouldBe(IsoDay(PaidOn));
        transaction.GetProperty("amount").GetInt64().ShouldBe(3_000);
        transaction.GetProperty("currency").GetString().ShouldBe("EUR");
        transaction.GetProperty("accountId").GetGuid().ShouldBe(trip.Konto.Id);
        transaction.GetProperty("accountAmount").GetInt64().ShouldBe(12_900);
        transaction.GetProperty("rate").GetDouble().ShouldBe(4.3);
        transaction.GetProperty("baseAmount").GetInt64().ShouldBe(12_900);
        transaction.GetProperty("categoryId").GetGuid().ShouldBe(groceries);
        transaction.GetProperty("title").GetString().ShouldBe("Market");
        (await LegsAsync(app, trip.UserId)).ShouldBeEmpty();
        (await BalanceAsync(user, trip.Konto.Id)).ShouldBe(87_100);
        (await FindItemAsync(user, repaymentId)).ShouldBeNull();
    }

    [Fact]
    public async Task Should_SplitAPartialPayment()
    {
        await using var app = await IsolatedAsync();
        using var owner = new AuthClient(app);
        using var user = new AuthClient(app);
        var trip = await SetUpAsync(app, owner, user);
        var groceries = await ExpenseCategoryIdAsync(user, "Groceries");
        var transport = await ExpenseCategoryIdAsync(user, "Transport");
        var first = await CreateRepaymentAsync(owner, trip, Repayment(trip.Anna, trip.Olya, 4_000, PaidOn));

        var firstItem = await ItemAsync(user, first);

        Lines(firstItem).ShouldBe([(trip.Market, "Market", 3_000L, "EUR"), (trip.Taxi, "Taxi", 1_000L, "EUR")]);
        firstItem.GetProperty("other").GetInt64().ShouldBe(0);
        firstItem.GetProperty("lines")[1].GetProperty("suggestion").GetProperty("categoryId").GetGuid().ShouldBe(transport);
        using (var confirmed = await ConfirmAsync(user, first, Suggested(firstItem, trip.Konto.Id)))
        {
            confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await SpendingAsync(user)).ShouldBe(
            [(groceries, "Market", IsoDay(PaidOn), 3_000L, 12_900L), (transport, "Taxi", IsoDay(PaidOn), 1_000L, 4_300L)],
            ignoreOrder: true);
        (await LegsAsync(app, trip.UserId)).ShouldBeEmpty();
        (await BalanceAsync(user, trip.Konto.Id)).ShouldBe(82_800);

        var second = await CreateRepaymentAsync(owner, trip, Repayment(trip.Anna, trip.Olya, 2_000, PaidOn));
        var secondItem = await ItemAsync(user, second);

        Lines(secondItem).ShouldBe([(trip.Taxi, "Taxi", 1_000L, "EUR")]);
        secondItem.GetProperty("other").GetInt64().ShouldBe(1_000);
        using (var confirmed = await ConfirmAsync(user, second, Suggested(secondItem, trip.Konto.Id)))
        {
            confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await SpendingAsync(user)).ShouldBe(
            [
                (groceries, "Market", IsoDay(PaidOn), 3_000L, 12_900L),
                (transport, "Taxi", IsoDay(PaidOn), 1_000L, 4_300L),
                (transport, "Taxi", IsoDay(PaidOn), 1_000L, 4_300L)
            ],
            ignoreOrder: true);
        (await LegsAsync(app, trip.UserId)).ShouldBe([(trip.Konto.Id, PaidOn, -4_300L)]);
        (await BalanceAsync(user, trip.Konto.Id)).ShouldBe(74_200);
    }

    [Fact]
    public async Task Should_SplitInTheRepaymentCurrency()
    {
        await using var app = await IsolatedAsync();
        using var owner = new AuthClient(app);
        using var user = new AuthClient(app);
        var trip = await SetUpAsync(app, owner, user);
        var repaymentId = await CreateRepaymentAsync(
            owner,
            trip,
            Repayment(trip.Anna, trip.Olya, 12_900, PaidOn, trip.Market) with { Currency = "PLN" });

        var item = await ItemAsync(user, repaymentId);

        var current = item.GetProperty("current");
        current.GetProperty("amount").GetInt64().ShouldBe(12_900);
        current.GetProperty("currency").GetString().ShouldBe("PLN");
        current.GetProperty("groupAmount").GetInt64().ShouldBe(3_000);
        Covers(current).ShouldBe([(trip.Market, 3_000L)]);
        Lines(item).ShouldBe([(trip.Market, "Market", 12_900L, "PLN")]);
        item.GetProperty("other").GetInt64().ShouldBe(0);

        using (var confirmed = await ConfirmAsync(user, repaymentId, Suggested(item, trip.Konto.Id)))
        {
            confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        var transaction = (await TransactionsAsync(user)).Single();
        transaction.GetProperty("date").GetString().ShouldBe(IsoDay(PaidOn));
        transaction.GetProperty("amount").GetInt64().ShouldBe(12_900);
        transaction.GetProperty("currency").GetString().ShouldBe("PLN");
        transaction.GetProperty("accountAmount").GetInt64().ShouldBe(12_900);
        transaction.GetProperty("rate").GetDouble().ShouldBe(1);
        transaction.GetProperty("baseAmount").GetInt64().ShouldBe(12_900);
        (await LegsAsync(app, trip.UserId)).ShouldBeEmpty();
        (await BalanceAsync(user, trip.Konto.Id)).ShouldBe(87_100);
    }

    [Fact]
    public async Task Should_SplitFreeFormPayment_InTheRepaymentCurrencyAcrossLines()
    {
        await using var app = await IsolatedAsync();
        using var owner = new AuthClient(app);
        using var user = new AuthClient(app);
        var trip = await SetUpAsync(app, owner, user);
        var groceries = await ExpenseCategoryIdAsync(user, "Groceries");
        var transport = await ExpenseCategoryIdAsync(user, "Transport");
        var repaymentId = await CreateRepaymentAsync(
            owner,
            trip,
            Repayment(trip.Anna, trip.Olya, 17_200, PaidOn) with { Currency = "PLN" });

        var item = await ItemAsync(user, repaymentId);

        item.GetProperty("current").GetProperty("groupAmount").GetInt64().ShouldBe(4_000);
        Covers(item.GetProperty("current")).ShouldBe([(trip.Market, 3_000L), (trip.Taxi, 1_000L)]);
        Lines(item).ShouldBe([(trip.Market, "Market", 12_900L, "PLN"), (trip.Taxi, "Taxi", 4_300L, "PLN")]);
        item.GetProperty("other").GetInt64().ShouldBe(0);

        using (var confirmed = await ConfirmAsync(user, repaymentId, Suggested(item, trip.Konto.Id)))
        {
            confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await SpendingAsync(user)).ShouldBe(
            [(groceries, "Market", IsoDay(PaidOn), 12_900L, 12_900L), (transport, "Taxi", IsoDay(PaidOn), 4_300L, 4_300L)],
            ignoreOrder: true);
        (await LegsAsync(app, trip.UserId)).ShouldBeEmpty();
        (await BalanceAsync(user, trip.Konto.Id)).ShouldBe(82_800);
    }

    [Fact]
    public async Task Should_NeverCountRepayments_AsIncome()
    {
        await using var app = await IsolatedAsync();
        using var owner = new AuthClient(app);
        using var user = new AuthClient(app);
        var trip = await SetUpAsync(app, owner, user);
        var overviewBefore = await GetAsync(user, "/v1/overview");
        var repaymentId = await CreateRepaymentAsync(owner, trip, Repayment(trip.Olya, trip.Anna, 4_000, Today));

        var item = await ItemAsync(user, repaymentId);

        var current = item.GetProperty("current");
        Names(current).ShouldBe(ReceivedTermProperties, ignoreOrder: true);
        current.GetProperty("direction").GetString().ShouldBe("received");
        current.GetProperty("memberId").GetGuid().ShouldBe(trip.Olya.Id);
        item.GetProperty("lines").GetArrayLength().ShouldBe(0);
        item.GetProperty("other").GetInt64().ShouldBe(4_000);

        using (var confirmed = await ConfirmAsync(user, repaymentId, Suggested(item, trip.Konto.Id)))
        {
            confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await TransactionsAsync(user)).ShouldBeEmpty();
        (await LegsAsync(app, trip.UserId)).ShouldBe([(trip.Konto.Id, Today, 17_200L)]);
        (await BalanceAsync(user, trip.Konto.Id)).ShouldBe(117_200);
        var overview = await GetAsync(user, "/v1/overview");
        overview.GetProperty("income").GetProperty("amount").GetInt64().ShouldBe(0);
        overview.GetProperty("expense").GetProperty("amount").GetInt64()
            .ShouldBe(overviewBefore.GetProperty("expense").GetProperty("amount").GetInt64());
        (await FindItemAsync(user, repaymentId)).ShouldBeNull();
    }

    [Fact]
    public async Task Should_FollowRepaymentChanges()
    {
        await using var app = await IsolatedAsync();
        using var owner = new AuthClient(app);
        using var user = new AuthClient(app);
        var trip = await SetUpAsync(app, owner, user);
        var groceries = await ExpenseCategoryIdAsync(user, "Groceries");
        var repaymentId = await CreateRepaymentAsync(owner, trip, Repayment(trip.Anna, trip.Olya, 3_000, PaidOn, trip.Market));
        using (var confirmed = await ConfirmAsync(user, repaymentId, Suggested(await ItemAsync(user, repaymentId), trip.Konto.Id)))
        {
            confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        await SendOkAsync(
            owner,
            HttpMethod.Put,
            RepaymentPath(trip, repaymentId),
            HttpStatusCode.OK,
            Repayment(trip.Anna, trip.Olya, 2_000, PaidOn, trip.Market));

        (await BookedAsync(user)).ShouldBe([(3_000L, 12_900L)]);
        (await BalanceAsync(user, trip.Konto.Id)).ShouldBe(87_100);
        var changed = await ItemAsync(user, repaymentId);
        changed.GetProperty("change").GetString().ShouldBe("changed");
        changed.GetProperty("previous").GetProperty("groupAmount").GetInt64().ShouldBe(3_000);
        Covers(changed.GetProperty("previous")).ShouldBe([(trip.Market, 3_000L)]);
        changed.GetProperty("current").GetProperty("groupAmount").GetInt64().ShouldBe(2_000);
        Covers(changed.GetProperty("current")).ShouldBe([(trip.Market, 2_000L)]);
        Lines(changed).ShouldBe([(trip.Market, "Market", 2_000L, "EUR")]);
        changed.GetProperty("suggestedAccountId").GetGuid().ShouldBe(trip.Konto.Id);

        using (var confirmed = await ConfirmAsync(user, repaymentId, Suggested(changed, trip.Konto.Id)))
        {
            confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await SpendingAsync(user)).ShouldBe([(groceries, "Market", IsoDay(PaidOn), 2_000L, 8_600L)]);
        (await LegsAsync(app, trip.UserId)).ShouldBeEmpty();
        (await BalanceAsync(user, trip.Konto.Id)).ShouldBe(91_400);

        await SendOkAsync(owner, HttpMethod.Delete, RepaymentPath(trip, repaymentId), HttpStatusCode.NoContent);

        var removed = await ItemAsync(user, repaymentId);
        removed.GetProperty("change").GetString().ShouldBe("removed");
        removed.GetProperty("current").ValueKind.ShouldBe(JsonValueKind.Null);
        Covers(removed.GetProperty("previous")).ShouldBe([(trip.Market, 2_000L)]);
        (await BookedAsync(user)).ShouldBe([(2_000L, 8_600L)]);

        using (var confirmed = await ConfirmAsync(user, repaymentId, new { version = Version(removed) }))
        {
            confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await TransactionsAsync(user)).ShouldBeEmpty();
        (await LegsAsync(app, trip.UserId)).ShouldBeEmpty();
        (await BalanceAsync(user, trip.Konto.Id)).ShouldBe(100_000);
        (await FindItemAsync(user, repaymentId)).ShouldBeNull();
    }

    [Fact]
    public async Task Should_BookUncoveredPayment_AsLeg()
    {
        await using var app = await IsolatedAsync();
        using var owner = new AuthClient(app);
        using var user = new AuthClient(app);
        var trip = await SetUpAsync(app, owner, user);
        var overviewBefore = await GetAsync(user, "/v1/overview");
        var repaymentId = await CreateRepaymentAsync(owner, trip, Repayment(trip.Anna, trip.Max, 5_000, Today));

        var item = await ItemAsync(user, repaymentId);

        var current = item.GetProperty("current");
        current.GetProperty("direction").GetString().ShouldBe("paid");
        current.GetProperty("groupAmount").GetInt64().ShouldBe(5_000);
        Covers(current).ShouldBeEmpty();
        item.GetProperty("lines").GetArrayLength().ShouldBe(0);
        item.GetProperty("other").GetInt64().ShouldBe(5_000);

        using (var confirmed = await ConfirmAsync(user, repaymentId, Suggested(item, trip.Konto.Id)))
        {
            confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await TransactionsAsync(user)).ShouldBeEmpty();
        (await LegsAsync(app, trip.UserId)).ShouldBe([(trip.Konto.Id, Today, -21_500L)]);
        (await BalanceAsync(user, trip.Konto.Id)).ShouldBe(78_500);
        var overview = await GetAsync(user, "/v1/overview");
        overview.GetProperty("income").GetProperty("amount").GetInt64().ShouldBe(0);
        overview.GetProperty("expense").GetProperty("amount").GetInt64()
            .ShouldBe(overviewBefore.GetProperty("expense").GetProperty("amount").GetInt64());
    }

    [Fact]
    public async Task Should_Return409_When_UserRelinkedToTheOtherMember()
    {
        await using var app = await IsolatedAsync();
        using var owner = new AuthClient(app);
        using var user = new AuthClient(app);
        var trip = await SetUpAsync(app, owner, user);
        var repaymentId = await CreateRepaymentAsync(owner, trip, Repayment(trip.Anna, trip.Olya, 3_000, PaidOn, trip.Market));
        var stale = await ItemAsync(user, repaymentId);
        stale.GetProperty("current").GetProperty("direction").GetString().ShouldBe("paid");

        await TestData.UnlinkMemberAsync(app, trip.Anna);
        await TestData.LinkMemberAsync(app, trip.Olya, trip.UserId);

        using (var confirmed = await ConfirmAsync(user, repaymentId, Suggested(stale, trip.Konto.Id)))
        {
            await ShouldHaveCodeAsync(confirmed, HttpStatusCode.Conflict, ItemChanged);
        }

        using (var rejected = await RejectAsync(user, repaymentId, Version(stale)))
        {
            await ShouldHaveCodeAsync(rejected, HttpStatusCode.Conflict, ItemChanged);
        }

        (await TransactionsAsync(user)).ShouldBeEmpty();
        (await LegsAsync(app, trip.UserId)).ShouldBeEmpty();
        (await ConfirmationCountAsync(app, trip.UserId)).ShouldBe(0);
        (await BalanceAsync(user, trip.Konto.Id)).ShouldBe(100_000);
        var received = await ItemAsync(user, repaymentId);
        received.GetProperty("current").GetProperty("direction").GetString().ShouldBe("received");
        received.GetProperty("current").GetProperty("memberId").GetGuid().ShouldBe(trip.Anna.Id);
        received.GetProperty("lines").GetArrayLength().ShouldBe(0);
        received.GetProperty("other").GetInt64().ShouldBe(3_000);
        Version(received).ShouldNotBe(Version(stale));

        using (var confirmed = await ConfirmAsync(user, repaymentId, Suggested(received, trip.Konto.Id)))
        {
            confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await TransactionsAsync(user)).ShouldBeEmpty();
        (await LegsAsync(app, trip.UserId)).ShouldBe([(trip.Konto.Id, PaidOn, 12_900L)]);
        (await BalanceAsync(user, trip.Konto.Id)).ShouldBe(112_900);
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
        var (anna, olya, max) = (members[0], members[1], members[2]);
        await TestData.LinkMemberAsync(app, anna, userId);
        var konto = await TestData.AddAccountAsync(app, userId, openingBalance: 100_000);
        var market = await CreateExpenseAsync(owner, group.Id, Expense(olya, 9_000, Today.AddDays(-5), "groceries", "Market"));
        var taxi = await CreateExpenseAsync(owner, group.Id, Expense(olya, 6_000, Today.AddDays(-3), "transport", "Taxi"));
        return new Trip(userId, group, anna, olya, max, konto, market, taxi);
    }

    private static object Expense(GroupMember payer, long amount, DateOnly date, string categoryId, string description) =>
        new
        {
            payerId = payer.Id,
            amount,
            currency = "EUR",
            date = IsoDay(date),
            categoryId,
            description
        };

    private static RepaymentBody Repayment(
        GroupMember from,
        GroupMember to,
        long amount,
        DateOnly date,
        params Guid[] settles) =>
        new(from.Id, to.Id, amount, "EUR", IsoDay(date), settles.Length == 0 ? null : settles);

    private static object Suggested(JsonElement item, Guid accountId) =>
        new
        {
            version = Version(item),
            accountId,
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

    private static string Version(JsonElement item) => item.GetProperty("version").GetString()!;

    private static string RepaymentPath(Trip trip, Guid repaymentId) => $"{GroupsPath}/{trip.Group.Id}/repayments/{repaymentId}";

    private static (Guid ExpenseId, long GroupAmount)[] Covers(JsonElement terms) =>
    [
        .. terms.GetProperty("covers").EnumerateArray()
            .Select(cover => (cover.GetProperty("expenseId").GetGuid(), cover.GetProperty("groupAmount").GetInt64()))
    ];

    private static (Guid ExpenseId, string? Description, long Amount, string? Currency)[] Lines(JsonElement item) =>
    [
        .. item.GetProperty("lines").EnumerateArray()
            .Select(line => (
                line.GetProperty("expenseId").GetGuid(),
                line.GetProperty("description").GetString(),
                line.GetProperty("amount").GetInt64(),
                line.GetProperty("currency").GetString()))
    ];

    private static Task<HttpResponseMessage> ConfirmAsync(AuthClient client, Guid entryId, object body) =>
        SendAsync(client, HttpMethod.Post, $"{ToConfirmPath}/repayments/{entryId}/confirm", body);

    private static Task<HttpResponseMessage> RejectAsync(AuthClient client, Guid entryId, string version) =>
        SendAsync(client, HttpMethod.Post, $"{ToConfirmPath}/repayments/{entryId}/reject", new { version });

    private static async Task<Guid> CreateExpenseAsync(AuthClient client, Guid groupId, object body)
    {
        using var response = await SendAsync(client, HttpMethod.Post, $"{GroupsPath}/{groupId}/expenses", body);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateRepaymentAsync(AuthClient client, Trip trip, RepaymentBody body)
    {
        using var response = await SendAsync(client, HttpMethod.Post, $"{GroupsPath}/{trip.Group.Id}/repayments", body);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

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
        foreach (var item in (await GetAsync(client, ToConfirmPath)).GetProperty("items").EnumerateArray())
        {
            if (item.GetProperty("entryId").GetGuid() == entryId)
            {
                return item;
            }
        }

        return null;
    }

    private static async Task<List<JsonElement>> TransactionsAsync(AuthClient client) =>
        [.. (await GetAsync(client, "/v1/transactions?pageSize=100")).GetProperty("items").EnumerateArray()];

    private static async Task<List<(long Amount, long AccountAmount)>> BookedAsync(AuthClient client) =>
        [
            .. (await TransactionsAsync(client)).Select(transaction => (
                transaction.GetProperty("amount").GetInt64(),
                transaction.GetProperty("accountAmount").GetInt64()))
        ];

    private static async Task<List<(Guid CategoryId, string? Title, string? Date, long Amount, long AccountAmount)>> SpendingAsync(
        AuthClient client) =>
        [
            .. (await TransactionsAsync(client)).Select(transaction =>
            {
                transaction.GetProperty("kind").GetString().ShouldBe("expense");
                return (
                    transaction.GetProperty("categoryId").GetGuid(),
                    transaction.GetProperty("title").GetString(),
                    transaction.GetProperty("date").GetString(),
                    transaction.GetProperty("amount").GetInt64(),
                    transaction.GetProperty("accountAmount").GetInt64());
            })
        ];

    private static async Task<long> BalanceAsync(AuthClient client, Guid accountId) =>
        (await GetAsync(client, "/v1/accounts")).GetProperty("items").EnumerateArray()
            .Single(account => account.GetProperty("id").GetGuid() == accountId)
            .GetProperty("balance").GetInt64();

    private static async Task<Guid> ExpenseCategoryIdAsync(AuthClient client, string name) =>
        (await GetAsync(client, "/v1/categories?kind=expense")).GetProperty("items").EnumerateArray()
            .Single(category => category.GetProperty("name").GetString() == name)
            .GetProperty("id").GetGuid();

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

    private static string[] Names(JsonElement value) => [.. value.EnumerateObject().Select(property => property.Name)];

    private static string?[] Strings(JsonElement array) => [.. array.EnumerateArray().Select(item => item.GetString())];

    private static string IsoDay(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private sealed record RepaymentBody(
        Guid FromMemberId,
        Guid ToMemberId,
        long Amount,
        string Currency,
        string Date,
        Guid[]? Settles);

    private sealed record Trip(
        Guid UserId,
        Group Group,
        GroupMember Anna,
        GroupMember Olya,
        GroupMember Max,
        Account Konto,
        Guid Market,
        Guid Taxi);
}
