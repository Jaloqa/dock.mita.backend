using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Debts;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Debts;

public sealed class DebtsTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string DebtsPath = "/v1/debts";
    private const string AccountsPath = "/v1/accounts";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Should_Return401_When_NotAuthenticated()
    {
        using var client = new AuthClient(factory);

        using var list = await client.Http.GetAsync(DebtsPath, Ct);
        using var create = await client.Http.PostAsJsonAsync(DebtsPath, new { }, Ct);

        list.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        create.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_CreateLentDebt_When_AccountGiven()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var account = await TestData.AddAccountAsync(factory, await UserIdAsync(client), openingBalance: 100_000);
        (await ListAsync(client, AccountsPath))[account.Id].GetProperty("balance").GetInt64().ShouldBe(100_000);

        using var response = await PostAsync(
            client,
            Body("lent", "  Max ", 50_000, "PLN", IsoDay(Today), dueDate: IsoDay(Today.AddDays(10)), accountId: account.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.EnumerateObject().Select(property => property.Name).ShouldBe(
            [
                "id", "direction", "counterparty", "amount", "currency", "repaid", "remaining", "status", "overdue", "date",
                "dueDate", "accountId", "accountAmount", "rate", "note", "repayments"
            ],
            ignoreOrder: true);
        var id = body.GetProperty("id").GetGuid();
        response.Headers.Location!.ToString().ShouldBe($"{DebtsPath}/{id}");
        body.GetProperty("direction").GetString().ShouldBe("lent");
        body.GetProperty("counterparty").GetString().ShouldBe("Max");
        body.GetProperty("amount").GetInt64().ShouldBe(50_000);
        body.GetProperty("currency").GetString().ShouldBe("PLN");
        body.GetProperty("repaid").GetInt64().ShouldBe(0);
        body.GetProperty("remaining").GetInt64().ShouldBe(50_000);
        body.GetProperty("status").GetString().ShouldBe("open");
        body.GetProperty("overdue").GetBoolean().ShouldBeFalse();
        body.GetProperty("date").GetString().ShouldBe(IsoDay(Today));
        body.GetProperty("dueDate").GetString().ShouldBe(IsoDay(Today.AddDays(10)));
        body.GetProperty("accountId").GetGuid().ShouldBe(account.Id);
        body.GetProperty("accountAmount").GetInt64().ShouldBe(50_000);
        body.GetProperty("rate").GetDouble().ShouldBe(1);
        body.GetProperty("repayments").GetInt32().ShouldBe(0);
        body.GetProperty("note").ValueKind.ShouldBe(JsonValueKind.Null);

        var accounts = await ListAsync(client, AccountsPath);
        accounts[account.Id].GetProperty("balance").GetInt64().ShouldBe(50_000);
        accounts[account.Id].GetProperty("inUse").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Should_CreateBorrowedDebt_When_AccountGiven()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var account = await TestData.AddAccountAsync(factory, await UserIdAsync(client), openingBalance: 100_000);

        using var response = await PostAsync(
            client,
            Body("borrowed", "Mum", 20_000, "PLN", IsoDay(Today), note: "  Rent  ", accountId: account.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("direction").GetString().ShouldBe("borrowed");
        body.GetProperty("note").GetString().ShouldBe("Rent");
        body.GetProperty("dueDate").ValueKind.ShouldBe(JsonValueKind.Null);
        (await ListAsync(client, AccountsPath))[account.Id].GetProperty("balance").GetInt64().ShouldBe(120_000);
    }

    [Fact]
    public async Task Should_KeepBalances_When_DebtHasNoAccount()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var account = await TestData.AddAccountAsync(factory, await UserIdAsync(client), openingBalance: 100_000);

        using var response = await PostAsync(client, Body("lent", "Max", 5_000, "EUR", IsoDay(Today), rate: 3));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("accountId").ValueKind.ShouldBe(JsonValueKind.Null);
        body.GetProperty("accountAmount").ValueKind.ShouldBe(JsonValueKind.Null);
        body.GetProperty("rate").ValueKind.ShouldBe(JsonValueKind.Null);
        body.GetProperty("currency").GetString().ShouldBe("EUR");
        (await ListAsync(client, AccountsPath))[account.Id].GetProperty("balance").GetInt64().ShouldBe(100_000);
    }

    [Fact]
    public async Task Should_PriceDebt_When_AccountCurrencyDiffers()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.EUR, IsoDay(Today), 4.2563m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var stored = await TestData.AddAccountAsync(isolated, userId);
        var manual = await TestData.AddAccountAsync(isolated, userId);

        using var automatic = await PostAsync(client, Body("lent", "Max", 5_000, "EUR", IsoDay(Today), accountId: stored.Id));
        using var overridden = await PostAsync(client, Body("lent", "Max", 5_000, "EUR", IsoDay(Today), rate: 4.5, accountId: manual.Id));

        automatic.StatusCode.ShouldBe(HttpStatusCode.Created);
        var automaticBody = await automatic.Content.ReadFromJsonAsync<JsonElement>(Ct);
        automaticBody.GetProperty("rate").GetDouble().ShouldBe(4.2563);
        automaticBody.GetProperty("accountAmount").GetInt64().ShouldBe(21_282);
        automaticBody.GetProperty("amount").GetInt64().ShouldBe(5_000);
        overridden.StatusCode.ShouldBe(HttpStatusCode.Created);
        var overriddenBody = await overridden.Content.ReadFromJsonAsync<JsonElement>(Ct);
        overriddenBody.GetProperty("rate").GetDouble().ShouldBe(4.5);
        overriddenBody.GetProperty("accountAmount").GetInt64().ShouldBe(22_500);

        var accounts = await ListAsync(client, AccountsPath);
        accounts[stored.Id].GetProperty("balance").GetInt64().ShouldBe(-21_282);
        accounts[manual.Id].GetProperty("balance").GetInt64().ShouldBe(-22_500);
    }

    [Fact]
    public async Task Should_Return503_When_DebtRateIsStale()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.EUR, IsoDay(Today.AddDays(-40)), 4.2563m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(isolated, userId);

        using var automatic = await PostAsync(client, Body("lent", "Max", 5_000, "EUR", IsoDay(Today), accountId: account.Id));

        automatic.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var problem = await automatic.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("code").GetString().ShouldBe("rates.unavailable");
        await using (var scope = isolated.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Debts.CountAsync(debt => debt.UserId == userId, Ct)).ShouldBe(0);
        }

        using var manual = await PostAsync(client, Body("lent", "Max", 5_000, "EUR", IsoDay(Today), rate: 4.5, accountId: account.Id));

        manual.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await manual.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("accountAmount").GetInt64().ShouldBe(22_500);
    }

    [Fact]
    public async Task Should_Return400_When_ConvertedAmountRoundsToZero()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.JPY, IsoDay(Today), 0.00001m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var account = await TestData.AddAccountAsync(isolated, await UserIdAsync(client));

        using var response = await PostAsync(client, Body("lent", "Max", 10, "JPY", IsoDay(Today), accountId: account.Id));

        await ShouldHaveErrorAsync(response, "rate", "validation.positiveAmount");
    }

    [Fact]
    public async Task Should_RefuseAccount_When_UnknownForeignOrArchived()
    {
        using var client = new AuthClient(factory);
        using var other = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        await other.RegisterNewAsync(Ct);
        var foreign = await TestData.AddAccountAsync(factory, await UserIdAsync(other));
        var archived = await TestData.AddAccountAsync(factory, await UserIdAsync(client), archived: true);

        using var foreignResponse = await PostAsync(client, Body("lent", "Max", 1_000, "PLN", IsoDay(Today), accountId: foreign.Id));
        using var unknownResponse = await PostAsync(client, Body("lent", "Max", 1_000, "PLN", IsoDay(Today), accountId: Guid.NewGuid()));
        using var archivedResponse = await PostAsync(client, Body("lent", "Max", 1_000, "PLN", IsoDay(Today), accountId: archived.Id));

        await ShouldHaveErrorAsync(foreignResponse, "accountId", "validation.unknownAccount");
        await ShouldHaveErrorAsync(unknownResponse, "accountId", "validation.unknownAccount");
        await ShouldHaveErrorAsync(archivedResponse, "accountId", "validation.archivedAccount");
    }

    [Fact]
    public async Task Should_ReportEveryInvalidField_When_BodyInvalid()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var response = await PostAsync(
            client,
            Body("gift", "   ", 0, "XDR", IsoDay(Today), dueDate: IsoDay(Today.AddDays(-1))));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        errors.EnumerateObject().Select(property => property.Name).ShouldBe(
            ["direction", "counterparty", "amount", "currency", "dueDate"],
            ignoreOrder: true);
        errors.GetProperty("dueDate")[0].GetString().ShouldBe("validation.dueBeforeDate");
    }

    [Fact]
    public async Task Should_Return400_When_BodyMissing()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var response = await client.Http.SendAsync(
            client.WithXsrf(new HttpRequestMessage(HttpMethod.Post, DebtsPath)),
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_ListOpenDebts_WithSummary()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.EUR, IsoDay(Today), 4.30m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var start = IsoDay(Today.AddDays(-10));
        var lentPln = await TestData.AddDebtAsync(
            isolated, userId, DebtDirection.Lent, 40_000, date: start, dueDate: IsoDay(Today.AddDays(5)));
        var lentEur = await TestData.AddDebtAsync(isolated, userId, DebtDirection.Lent, 5_000, Currency.EUR, date: start);
        var borrowed = await TestData.AddDebtAsync(
            isolated, userId, DebtDirection.Borrowed, 150_000, date: start, dueDate: IsoDay(Today.AddDays(-1)));
        var settled = await TestData.AddDebtAsync(isolated, userId, DebtDirection.Lent, 12_000, date: start);
        await TestData.AddRepaymentAsync(isolated, settled, 12_000, date: IsoDay(Today));

        using var open = await client.Http.GetAsync(DebtsPath, Ct);

        open.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await open.Content.ReadFromJsonAsync<JsonElement>(Ct);
        page.EnumerateObject().Select(property => property.Name).ShouldBe(
            ["items", "page", "pageSize", "total", "currency", "summary"],
            ignoreOrder: true);
        page.GetProperty("page").GetInt32().ShouldBe(1);
        page.GetProperty("pageSize").GetInt32().ShouldBe(100);
        page.GetProperty("total").GetInt32().ShouldBe(3);
        page.GetProperty("currency").GetString().ShouldBe("PLN");
        var items = page.GetProperty("items").EnumerateArray().ToList();
        items.Select(item => item.GetProperty("id").GetGuid()).ShouldBe([borrowed.Id, lentPln.Id, lentEur.Id]);
        items[0].GetProperty("overdue").GetBoolean().ShouldBeTrue();
        items[0].GetProperty("direction").GetString().ShouldBe("borrowed");
        items[1].GetProperty("overdue").GetBoolean().ShouldBeFalse();
        items[2].GetProperty("overdue").GetBoolean().ShouldBeFalse();
        items[2].GetProperty("dueDate").ValueKind.ShouldBe(JsonValueKind.Null);
        items[2].GetProperty("currency").GetString().ShouldBe("EUR");
        ShouldHaveSummary(page.GetProperty("summary"), 61_500, 150_000, 3, 1);

        using var closed = await client.Http.GetAsync($"{DebtsPath}?status=settled", Ct);

        closed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var closedPage = await closed.Content.ReadFromJsonAsync<JsonElement>(Ct);
        closedPage.GetProperty("total").GetInt32().ShouldBe(1);
        var only = closedPage.GetProperty("items").EnumerateArray().Single();
        only.GetProperty("id").GetGuid().ShouldBe(settled.Id);
        only.GetProperty("status").GetString().ShouldBe("settled");
        only.GetProperty("remaining").GetInt64().ShouldBe(0);
        only.GetProperty("repaid").GetInt64().ShouldBe(12_000);
        only.GetProperty("overdue").GetBoolean().ShouldBeFalse();
        only.GetProperty("repayments").GetInt32().ShouldBe(1);
        ShouldHaveSummary(closedPage.GetProperty("summary"), 61_500, 150_000, 3, 1);
    }

    [Fact]
    public async Task Should_OrderDebts_When_DueDatesTie()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var due = IsoDay(Today.AddDays(30));
        var older = await TestData.AddDebtAsync(factory, userId, DebtDirection.Lent, 1_000, date: IsoDay(Today.AddDays(-20)), dueDate: due);
        var newer = await TestData.AddDebtAsync(factory, userId, DebtDirection.Lent, 2_000, date: IsoDay(Today.AddDays(-5)), dueDate: due);
        var sooner = await TestData.AddDebtAsync(
            factory, userId, DebtDirection.Lent, 3_000, date: IsoDay(Today.AddDays(-30)), dueDate: IsoDay(Today.AddDays(1)));
        var undated = await TestData.AddDebtAsync(factory, userId, DebtDirection.Borrowed, 4_000, date: IsoDay(Today));

        using var response = await client.Http.GetAsync(DebtsPath, Ct);

        var items = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items");
        items.EnumerateArray().Select(item => item.GetProperty("id").GetGuid())
            .ShouldBe([sooner.Id, newer.Id, older.Id, undated.Id]);
    }

    [Fact]
    public async Task Should_PageDebts_And_SummarizeEveryOpenDebt()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        for (var index = 0; index < 3; index++)
        {
            await TestData.AddDebtAsync(factory, userId, DebtDirection.Lent, 1_000, date: IsoDay(Today.AddDays(-index)));
        }

        using var response = await client.Http.GetAsync($"{DebtsPath}?page=2&pageSize=2", Ct);

        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        page.GetProperty("total").GetInt32().ShouldBe(3);
        page.GetProperty("page").GetInt32().ShouldBe(2);
        page.GetProperty("pageSize").GetInt32().ShouldBe(2);
        page.GetProperty("items").GetArrayLength().ShouldBe(1);
        ShouldHaveSummary(page.GetProperty("summary"), 3_000, 0, 3, 0);
    }

    [Fact]
    public async Task Should_ReportInvalidValue_When_StatusOrPagingInvalid()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var status = await client.Http.GetAsync($"{DebtsPath}?status=Open", Ct);
        using var pageSize = await client.Http.GetAsync($"{DebtsPath}?pageSize=101", Ct);

        await ShouldHaveErrorAsync(status, "status", "validation.invalidValue");
        await ShouldHaveErrorAsync(pageSize, "pageSize", "validation.invalidValue");
    }

    [Fact]
    public async Task Should_Return503_When_SummaryNeedsMissingRate()
    {
        await using var isolated = factory.WithIsolatedData();
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var debt = await TestData.AddDebtAsync(isolated, userId, DebtDirection.Lent, 5_000, Currency.KWD, date: IsoDay(Today));

        using var missing = await client.Http.GetAsync(DebtsPath, Ct);

        missing.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await missing.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString().ShouldBe("rates.unavailable");

        await TestData.AddRepaymentAsync(isolated, debt, 5_000);

        using var settled = await client.Http.GetAsync(DebtsPath, Ct);

        settled.StatusCode.ShouldBe(HttpStatusCode.OK);
        ShouldHaveSummary((await settled.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("summary"), 0, 0, 0, 0);
    }

    [Fact]
    public async Task Should_IsolateUsers()
    {
        using var owner = new AuthClient(factory);
        using var intruder = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        await intruder.RegisterNewAsync(Ct);
        var account = await TestData.AddAccountAsync(factory, await UserIdAsync(owner), openingBalance: 10_000);
        await TestData.AddDebtAsync(factory, await UserIdAsync(owner), DebtDirection.Lent, 3_000, account: account, date: IsoDay(Today));
        await TestData.AddDebtAsync(factory, await UserIdAsync(owner), DebtDirection.Borrowed, 1_000, date: IsoDay(Today));

        using var ownerList = await owner.Http.GetAsync(DebtsPath, Ct);
        using var intruderList = await intruder.Http.GetAsync(DebtsPath, Ct);
        using var intruderSettled = await intruder.Http.GetAsync($"{DebtsPath}?status=settled", Ct);

        var ownerPage = await ownerList.Content.ReadFromJsonAsync<JsonElement>(Ct);
        ownerPage.GetProperty("total").GetInt32().ShouldBe(2);
        ShouldHaveSummary(ownerPage.GetProperty("summary"), 3_000, 1_000, 2, 0);
        foreach (var response in new[] { intruderList, intruderSettled })
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var page = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            page.GetProperty("total").GetInt32().ShouldBe(0);
            page.GetProperty("items").GetArrayLength().ShouldBe(0);
            ShouldHaveSummary(page.GetProperty("summary"), 0, 0, 0, 0);
        }
    }

    private static void ShouldHaveSummary(JsonElement summary, long owedToMe, long owedByMe, int open, int overdue)
    {
        summary.EnumerateObject().Select(property => property.Name).ShouldBe(
            ["owedToMe", "owedByMe", "open", "overdue", "groups"],
            ignoreOrder: true);
        summary.GetProperty("owedToMe").GetInt64().ShouldBe(owedToMe);
        summary.GetProperty("owedByMe").GetInt64().ShouldBe(owedByMe);
        summary.GetProperty("open").GetInt32().ShouldBe(open);
        summary.GetProperty("overdue").GetInt32().ShouldBe(overdue);
        summary.GetProperty("groups").GetInt32().ShouldBe(0);
    }

    private static Dictionary<string, object?> Body(
        string direction,
        string counterparty,
        long amount,
        string currency,
        string date,
        string? dueDate = null,
        Guid? accountId = null,
        double? rate = null,
        string? note = null) =>
        new()
        {
            ["direction"] = direction,
            ["counterparty"] = counterparty,
            ["amount"] = amount,
            ["currency"] = currency,
            ["date"] = date,
            ["dueDate"] = dueDate,
            ["accountId"] = accountId,
            ["rate"] = rate,
            ["note"] = note
        };

    private static string IsoDay(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static Task<HttpResponseMessage> PostAsync(AuthClient client, object body) =>
        client.Http.SendAsync(
            client.WithXsrf(new HttpRequestMessage(HttpMethod.Post, DebtsPath) { Content = JsonContent.Create(body) }),
            Ct);

    private static async Task<Dictionary<Guid, JsonElement>> ListAsync(AuthClient client, string path)
    {
        using var response = await client.Http.GetAsync(path, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return page.GetProperty("items").EnumerateArray().ToDictionary(item => item.GetProperty("id").GetGuid());
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
