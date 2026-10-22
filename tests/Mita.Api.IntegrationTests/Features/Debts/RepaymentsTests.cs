using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Accounts;
using Mita.Domain.Debts;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Debts;

public sealed class RepaymentsTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string DebtsPath = "/v1/debts";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Should_Return401_When_NotAuthenticated()
    {
        using var client = new AuthClient(factory);
        var path = RepaymentsPath(Guid.NewGuid());

        using var list = await client.Http.GetAsync(path, Ct);
        using var add = await client.Http.PostAsJsonAsync(path, new { }, Ct);
        using var delete = await client.Http.DeleteAsync($"{path}/{Guid.NewGuid()}", Ct);

        list.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        add.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        delete.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_ReduceRemaining_And_MoveMoney_When_RepaymentRecorded()
    {
        using var client = new AuthClient(factory);
        var (account, debt) = await LendAsync(factory, client);
        (await BalancesAsync(client))[account.Id].ShouldBe(50_000);

        using var response = await SendAsync(client, HttpMethod.Post, RepaymentsPath(debt.Id), Body(10_000, IsoDay(Today), accountId: account.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Location.ShouldBeNull();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.EnumerateObject().Select(property => property.Name).ShouldBe(
            [
                "id", "direction", "counterparty", "amount", "currency", "repaid", "remaining", "status", "overdue", "date",
                "dueDate", "accountId", "accountAmount", "rate", "note", "repayments"
            ],
            ignoreOrder: true);
        body.GetProperty("id").GetGuid().ShouldBe(debt.Id);
        body.GetProperty("amount").GetInt64().ShouldBe(50_000);
        body.GetProperty("repaid").GetInt64().ShouldBe(10_000);
        body.GetProperty("remaining").GetInt64().ShouldBe(40_000);
        body.GetProperty("repayments").GetInt32().ShouldBe(1);
        body.GetProperty("status").GetString().ShouldBe("open");
        (await BalancesAsync(client))[account.Id].ShouldBe(60_000);
    }

    [Fact]
    public async Task Should_Return400_When_RepaymentExceedsRemaining()
    {
        using var client = new AuthClient(factory);
        var (account, debt) = await LendAsync(factory, client);
        using var first = await SendAsync(client, HttpMethod.Post, RepaymentsPath(debt.Id), Body(10_000, IsoDay(Today), accountId: account.Id));
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var response = await SendAsync(client, HttpMethod.Post, RepaymentsPath(debt.Id), Body(40_001, IsoDay(Today), accountId: account.Id));

        await ShouldHaveErrorAsync(response, "amount", "validation.exceedsRemaining");
        (await RepaymentCountAsync(factory, debt.Id)).ShouldBe(1);
        (await RepaidAsync(factory, debt.Id)).ShouldBe(10_000);
        (await BalancesAsync(client))[account.Id].ShouldBe(60_000);
    }

    [Fact]
    public async Task Should_AcceptOnlyOneRepayment_When_TwoRequestsRaceForRemaining()
    {
        using var client = new AuthClient(factory);
        var (account, debt) = await LendAsync(factory, client);
        var path = RepaymentsPath(debt.Id);

        var responses = await Task.WhenAll(
            SendAsync(client, HttpMethod.Post, path, Body(30_000, IsoDay(Today), accountId: account.Id)),
            SendAsync(client, HttpMethod.Post, path, Body(30_000, IsoDay(Today), accountId: account.Id)));

        try
        {
            responses.Select(response => response.StatusCode).Order().ShouldBe([HttpStatusCode.OK, HttpStatusCode.BadRequest]);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        (await RepaidAsync(factory, debt.Id)).ShouldBe(30_000);
        (await RepaymentCountAsync(factory, debt.Id)).ShouldBe(1);
        (await BalancesAsync(client))[account.Id].ShouldBe(80_000);
    }

    [Fact]
    public async Task Should_SettleDebt_When_RepaidInFull()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var debt = await TestData.AddDebtAsync(
            factory,
            userId,
            DebtDirection.Lent,
            50_000,
            date: IsoDay(Today.AddDays(-10)),
            dueDate: IsoDay(Today.AddDays(-1)));
        var before = await DebtPageAsync(client, string.Empty);
        before.GetProperty("items")[0].GetProperty("overdue").GetBoolean().ShouldBeTrue();
        ShouldHaveSummary(before.GetProperty("summary"), 50_000, 0, 1, 1);

        using var response = await SendAsync(client, HttpMethod.Post, RepaymentsPath(debt.Id), Body(50_000, IsoDay(Today)));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("status").GetString().ShouldBe("settled");
        body.GetProperty("overdue").GetBoolean().ShouldBeFalse();
        body.GetProperty("remaining").GetInt64().ShouldBe(0);
        body.GetProperty("repaid").GetInt64().ShouldBe(50_000);
        var open = await DebtPageAsync(client, string.Empty);
        open.GetProperty("total").GetInt32().ShouldBe(0);
        ShouldHaveSummary(open.GetProperty("summary"), 0, 0, 0, 0);
        var settled = await DebtPageAsync(client, "?status=settled");
        settled.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ShouldBe([debt.Id]);

        using var extra = await SendAsync(client, HttpMethod.Post, RepaymentsPath(debt.Id), Body(1, IsoDay(Today)));

        await ShouldHaveErrorAsync(extra, "amount", "validation.exceedsRemaining");
        (await RepaymentCountAsync(factory, debt.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task Should_ConvertRepayment_When_CurrencyDiffers()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.EUR, IsoDay(Today), 4.2563m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var euro = await TestData.AddAccountAsync(isolated, userId, Currency.EUR);
        var zloty = await TestData.AddAccountAsync(isolated, userId);
        var intoEuro = await TestData.AddDebtAsync(isolated, userId, DebtDirection.Lent, 50_000, date: IsoDay(Today));
        var intoZloty = await TestData.AddDebtAsync(isolated, userId, DebtDirection.Lent, 50_000, date: IsoDay(Today));
        var overridden = await TestData.AddDebtAsync(isolated, userId, DebtDirection.Lent, 50_000, date: IsoDay(Today));

        using var toEuro = await SendAsync(client, HttpMethod.Post, RepaymentsPath(intoEuro.Id), Body(5_000, IsoDay(Today), "EUR", euro.Id));
        using var toZloty = await SendAsync(client, HttpMethod.Post, RepaymentsPath(intoZloty.Id), Body(5_000, IsoDay(Today), "EUR", zloty.Id));
        using var manual = await SendAsync(
            client,
            HttpMethod.Post,
            RepaymentsPath(overridden.Id),
            Body(5_000, IsoDay(Today), "EUR", zloty.Id, rate: 4.0));

        toEuro.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await toEuro.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("repaid").GetInt64().ShouldBe(21_282);
        var euroRow = await SingleRepaymentAsync(client, intoEuro.Id);
        euroRow.GetProperty("amount").GetInt64().ShouldBe(5_000);
        euroRow.GetProperty("currency").GetString().ShouldBe("EUR");
        euroRow.GetProperty("rate").GetDouble().ShouldBe(4.2563);
        euroRow.GetProperty("debtAmount").GetInt64().ShouldBe(21_282);
        euroRow.GetProperty("accountId").GetGuid().ShouldBe(euro.Id);
        euroRow.GetProperty("accountAmount").GetInt64().ShouldBe(5_000);

        toZloty.StatusCode.ShouldBe(HttpStatusCode.OK);
        var zlotyRow = await SingleRepaymentAsync(client, intoZloty.Id);
        zlotyRow.GetProperty("debtAmount").GetInt64().ShouldBe(21_282);
        zlotyRow.GetProperty("accountAmount").GetInt64().ShouldBe(21_282);

        manual.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await manual.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("repaid").GetInt64().ShouldBe(20_000);
        var manualRow = await SingleRepaymentAsync(client, overridden.Id);
        manualRow.GetProperty("rate").GetDouble().ShouldBe(4.0);
        manualRow.GetProperty("debtAmount").GetInt64().ShouldBe(20_000);
        manualRow.GetProperty("accountAmount").GetInt64().ShouldBe(20_000);

        var balances = await BalancesAsync(client);
        balances[euro.Id].ShouldBe(5_000);
        balances[zloty.Id].ShouldBe(41_282);
    }

    [Fact]
    public async Task Should_UseDateRate_When_AccountInThirdCurrency()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.EUR, IsoDay(Today), 4.2563m), (Currency.USD, IsoDay(Today), 3.9012m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var dollar = await TestData.AddAccountAsync(isolated, userId, Currency.USD);
        var automatic = await TestData.AddDebtAsync(isolated, userId, DebtDirection.Lent, 50_000, date: IsoDay(Today));
        var overridden = await TestData.AddDebtAsync(isolated, userId, DebtDirection.Lent, 50_000, date: IsoDay(Today));

        using var withoutRate = await SendAsync(client, HttpMethod.Post, RepaymentsPath(automatic.Id), Body(5_000, IsoDay(Today), "EUR", dollar.Id));
        using var withRate = await SendAsync(
            client,
            HttpMethod.Post,
            RepaymentsPath(overridden.Id),
            Body(5_000, IsoDay(Today), "EUR", dollar.Id, rate: 4.0));

        withoutRate.StatusCode.ShouldBe(HttpStatusCode.OK);
        withRate.StatusCode.ShouldBe(HttpStatusCode.OK);
        var automaticRow = await SingleRepaymentAsync(client, automatic.Id);
        automaticRow.GetProperty("debtAmount").GetInt64().ShouldBe(21_282);
        automaticRow.GetProperty("accountAmount").GetInt64().ShouldBe(5_455);
        var overriddenRow = await SingleRepaymentAsync(client, overridden.Id);
        overriddenRow.GetProperty("debtAmount").GetInt64().ShouldBe(20_000);
        overriddenRow.GetProperty("accountAmount").GetInt64().ShouldBe(5_455);
        (await BalancesAsync(client))[dollar.Id].ShouldBe(10_910);
    }

    [Fact]
    public async Task Should_MoveMoneyOut_When_BorrowedDebtRepaid()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(factory, userId);
        var debt = await TestData.AddDebtAsync(
            factory,
            userId,
            DebtDirection.Borrowed,
            100_000,
            account: account,
            date: IsoDay(Today));
        (await BalancesAsync(client))[account.Id].ShouldBe(100_000);

        using var response = await SendAsync(client, HttpMethod.Post, RepaymentsPath(debt.Id), Body(30_000, IsoDay(Today), accountId: account.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("remaining").GetInt64().ShouldBe(70_000);
        (await BalancesAsync(client))[account.Id].ShouldBe(70_000);
    }

    [Fact]
    public async Task Should_KeepBalances_When_RepaymentHasNoAccount()
    {
        using var client = new AuthClient(factory);
        var (account, debt) = await LendAsync(factory, client);

        using var response = await SendAsync(client, HttpMethod.Post, RepaymentsPath(debt.Id), Body(10_000, IsoDay(Today)));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var row = await SingleRepaymentAsync(client, debt.Id);
        row.GetProperty("accountId").ValueKind.ShouldBe(JsonValueKind.Null);
        row.GetProperty("accountAmount").ValueKind.ShouldBe(JsonValueKind.Null);
        (await BalancesAsync(client))[account.Id].ShouldBe(50_000);
    }

    [Fact]
    public async Task Should_Return503_When_RepaymentRateIsStale()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.EUR, IsoDay(Today.AddDays(-40)), 4.2563m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(isolated, userId);
        var debt = await TestData.AddDebtAsync(isolated, userId, DebtDirection.Lent, 50_000, date: IsoDay(Today));

        using var automatic = await SendAsync(client, HttpMethod.Post, RepaymentsPath(debt.Id), Body(5_000, IsoDay(Today), "EUR", account.Id));

        automatic.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var problem = await automatic.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("code").GetString().ShouldBe("rates.unavailable");
        (await RepaymentCountAsync(isolated, debt.Id)).ShouldBe(0);
        (await RepaidAsync(isolated, debt.Id)).ShouldBe(0);

        using var manual = await SendAsync(
            client,
            HttpMethod.Post,
            RepaymentsPath(debt.Id),
            Body(5_000, IsoDay(Today), "EUR", account.Id, rate: 4.5));

        manual.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await manual.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("repaid").GetInt64().ShouldBe(22_500);
    }

    [Fact]
    public async Task Should_Return400_When_ConvertedAmountRoundsToZero()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.JPY, IsoDay(Today), 0.03m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var yen = await TestData.AddAccountAsync(isolated, userId, Currency.JPY);
        var debt = await TestData.AddDebtAsync(isolated, userId, DebtDirection.Lent, 50_000, date: IsoDay(Today));

        using var debtAmount = await SendAsync(client, HttpMethod.Post, RepaymentsPath(debt.Id), Body(10, IsoDay(Today), "JPY", rate: 0.00001));
        using var accountAmount = await SendAsync(client, HttpMethod.Post, RepaymentsPath(debt.Id), Body(1, IsoDay(Today), "PLN", yen.Id));

        await ShouldHaveErrorAsync(debtAmount, "rate", "validation.positiveAmount");
        await ShouldHaveErrorAsync(accountAmount, "rate", "validation.positiveAmount");
        (await RepaymentCountAsync(isolated, debt.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task Should_Return400_When_ConvertedAmountExceedsMaximum()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var debt = await TestData.AddDebtAsync(factory, await UserIdAsync(client), DebtDirection.Lent, 50_000, date: IsoDay(Today));

        using var response = await SendAsync(
            client,
            HttpMethod.Post,
            RepaymentsPath(debt.Id),
            Body(100_000, IsoDay(Today), "EUR", rate: 1_000_000_000));

        await ShouldHaveErrorAsync(response, "rate", "validation.invalidValue");
        (await RepaymentCountAsync(factory, debt.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task Should_ListRepayments_NewestFirst()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.EUR, IsoDay(Today.AddDays(-5)), 4.2563m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(isolated, userId);
        var debt = await TestData.AddDebtAsync(isolated, userId, DebtDirection.Lent, 100_000, date: IsoDay(Today.AddDays(-30)));
        using var oldest = await SendAsync(
            client,
            HttpMethod.Post,
            RepaymentsPath(debt.Id),
            Body(1_000, IsoDay(Today.AddDays(-2)), note: "  first  "));
        using var middle = await SendAsync(client, HttpMethod.Post, RepaymentsPath(debt.Id), Body(2_000, IsoDay(Today.AddDays(-1)), accountId: account.Id));
        using var newest = await SendAsync(client, HttpMethod.Post, RepaymentsPath(debt.Id), Body(5_000, IsoDay(Today), "EUR", account.Id));
        oldest.StatusCode.ShouldBe(HttpStatusCode.OK);
        middle.StatusCode.ShouldBe(HttpStatusCode.OK);
        newest.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var response = await client.Http.GetAsync(RepaymentsPath(debt.Id), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        page.EnumerateObject().Select(property => property.Name).ShouldBe(
            ["items", "page", "pageSize", "total"],
            ignoreOrder: true);
        page.GetProperty("page").GetInt32().ShouldBe(1);
        page.GetProperty("pageSize").GetInt32().ShouldBe(100);
        page.GetProperty("total").GetInt32().ShouldBe(3);
        var items = page.GetProperty("items").EnumerateArray().ToList();
        items[0].EnumerateObject().Select(property => property.Name).ShouldBe(
            ["id", "date", "amount", "currency", "rate", "debtAmount", "accountId", "accountAmount", "note"],
            ignoreOrder: true);

        items[0].GetProperty("date").GetString().ShouldBe(IsoDay(Today));
        items[0].GetProperty("amount").GetInt64().ShouldBe(5_000);
        items[0].GetProperty("currency").GetString().ShouldBe("EUR");
        items[0].GetProperty("rate").GetDouble().ShouldBe(4.2563);
        items[0].GetProperty("debtAmount").GetInt64().ShouldBe(21_282);
        items[0].GetProperty("accountId").GetGuid().ShouldBe(account.Id);
        items[0].GetProperty("accountAmount").GetInt64().ShouldBe(21_282);
        items[0].GetProperty("note").ValueKind.ShouldBe(JsonValueKind.Null);

        items[1].GetProperty("date").GetString().ShouldBe(IsoDay(Today.AddDays(-1)));
        items[1].GetProperty("amount").GetInt64().ShouldBe(2_000);
        items[1].GetProperty("currency").GetString().ShouldBe("PLN");
        items[1].GetProperty("rate").GetDouble().ShouldBe(1);
        items[1].GetProperty("debtAmount").GetInt64().ShouldBe(2_000);
        items[1].GetProperty("accountId").GetGuid().ShouldBe(account.Id);
        items[1].GetProperty("accountAmount").GetInt64().ShouldBe(2_000);

        items[2].GetProperty("date").GetString().ShouldBe(IsoDay(Today.AddDays(-2)));
        items[2].GetProperty("amount").GetInt64().ShouldBe(1_000);
        items[2].GetProperty("debtAmount").GetInt64().ShouldBe(1_000);
        items[2].GetProperty("accountId").ValueKind.ShouldBe(JsonValueKind.Null);
        items[2].GetProperty("accountAmount").ValueKind.ShouldBe(JsonValueKind.Null);
        items[2].GetProperty("note").GetString().ShouldBe("first");

        using var paged = await client.Http.GetAsync($"{RepaymentsPath(debt.Id)}?pageSize=2", Ct);

        var firstPage = await paged.Content.ReadFromJsonAsync<JsonElement>(Ct);
        firstPage.GetProperty("total").GetInt32().ShouldBe(3);
        firstPage.GetProperty("pageSize").GetInt32().ShouldBe(2);
        firstPage.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())
            .ShouldBe(items.Take(2).Select(item => item.GetProperty("id").GetGuid()));

        using var second = await client.Http.GetAsync($"{RepaymentsPath(debt.Id)}?page=2&pageSize=2", Ct);

        var secondPage = await second.Content.ReadFromJsonAsync<JsonElement>(Ct);
        secondPage.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())
            .ShouldBe([items[2].GetProperty("id").GetGuid()]);
    }

    [Fact]
    public async Task Should_ReportInvalidValue_When_PagingInvalid()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var pageSize = await client.Http.GetAsync($"{RepaymentsPath(Guid.NewGuid())}?pageSize=101", Ct);
        using var page = await client.Http.GetAsync($"{RepaymentsPath(Guid.NewGuid())}?page=0", Ct);

        await ShouldHaveErrorAsync(pageSize, "pageSize", "validation.invalidValue");
        await ShouldHaveErrorAsync(page, "page", "validation.invalidValue");
    }

    [Fact]
    public async Task Should_RestoreDebt_And_Balance_When_RepaymentDeleted()
    {
        using var client = new AuthClient(factory);
        var (account, debt) = await LendAsync(factory, client);
        var other = await TestData.AddDebtAsync(factory, debt.UserId, DebtDirection.Lent, 9_000, date: IsoDay(Today));
        var foreign = await TestData.AddRepaymentAsync(factory, other, 4_000, date: IsoDay(Today));
        using var added = await SendAsync(client, HttpMethod.Post, RepaymentsPath(debt.Id), Body(10_000, IsoDay(Today), accountId: account.Id));
        added.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await BalancesAsync(client))[account.Id].ShouldBe(60_000);
        var repaymentId = (await SingleRepaymentAsync(client, debt.Id)).GetProperty("id").GetGuid();

        using var wrongDebt = await SendAsync(client, HttpMethod.Delete, $"{RepaymentsPath(debt.Id)}/{foreign.Id}", null);

        wrongDebt.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await RepaymentCountAsync(factory, other.Id)).ShouldBe(1);
        (await RepaidAsync(factory, other.Id)).ShouldBe(4_000);

        using var deleted = await SendAsync(client, HttpMethod.Delete, $"{RepaymentsPath(debt.Id)}/{repaymentId}", null);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var item = (await DebtPageAsync(client, string.Empty)).GetProperty("items").EnumerateArray()
            .Single(candidate => candidate.GetProperty("id").GetGuid() == debt.Id);
        item.GetProperty("repaid").GetInt64().ShouldBe(0);
        item.GetProperty("remaining").GetInt64().ShouldBe(50_000);
        item.GetProperty("repayments").GetInt32().ShouldBe(0);
        item.GetProperty("status").GetString().ShouldBe("open");
        (await BalancesAsync(client))[account.Id].ShouldBe(50_000);
        (await RepaymentCountAsync(factory, debt.Id)).ShouldBe(0);

        using var again = await SendAsync(client, HttpMethod.Delete, $"{RepaymentsPath(debt.Id)}/{repaymentId}", null);

        again.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_ReopenSettledDebt_When_RepaymentDeleted()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var debt = await TestData.AddDebtAsync(factory, await UserIdAsync(client), DebtDirection.Borrowed, 8_000, date: IsoDay(Today));
        var repayment = await TestData.AddRepaymentAsync(factory, debt, 8_000, date: IsoDay(Today));

        using var response = await SendAsync(client, HttpMethod.Delete, $"{RepaymentsPath(debt.Id)}/{repayment.Id}", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var open = await DebtPageAsync(client, string.Empty);
        open.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ShouldBe([debt.Id]);
        ShouldHaveSummary(open.GetProperty("summary"), 0, 8_000, 1, 0);
    }

    [Fact]
    public async Task Should_Return404_When_DebtBelongsToAnotherUser()
    {
        using var owner = new AuthClient(factory);
        using var intruder = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        await intruder.RegisterNewAsync(Ct);
        var ownerId = await UserIdAsync(owner);
        var account = await TestData.AddAccountAsync(factory, ownerId, openingBalance: 10_000);
        var debt = await TestData.AddDebtAsync(factory, ownerId, DebtDirection.Lent, 3_000, account: account, date: IsoDay(Today));
        var repayment = await TestData.AddRepaymentAsync(factory, debt, 1_000, account, IsoDay(Today));
        var intruderAccount = await TestData.AddAccountAsync(factory, await UserIdAsync(intruder));

        using var add = await SendAsync(
            intruder,
            HttpMethod.Post,
            RepaymentsPath(debt.Id),
            Body(500, IsoDay(Today), accountId: intruderAccount.Id));
        using var list = await intruder.Http.GetAsync(RepaymentsPath(debt.Id), Ct);
        using var delete = await SendAsync(intruder, HttpMethod.Delete, $"{RepaymentsPath(debt.Id)}/{repayment.Id}", null);

        add.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        list.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        delete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await RepaymentCountAsync(factory, debt.Id)).ShouldBe(1);
        (await RepaidAsync(factory, debt.Id)).ShouldBe(1_000);
        (await BalancesAsync(owner))[account.Id].ShouldBe(8_000);
        (await BalancesAsync(intruder))[intruderAccount.Id].ShouldBe(0);
        using var ownerList = await owner.Http.GetAsync(RepaymentsPath(debt.Id), Ct);
        (await ownerList.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("total").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Return400_BeforeLookup_When_BodyInvalid()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var unknown = Guid.NewGuid();

        using var invalid = await SendAsync(client, HttpMethod.Post, RepaymentsPath(unknown), Body(0, IsoDay(Today)));
        using var valid = await SendAsync(client, HttpMethod.Post, RepaymentsPath(unknown), Body(1_000, IsoDay(Today)));

        await ShouldHaveErrorAsync(invalid, "amount", "validation.positiveAmount");
        valid.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_ReportEveryInvalidField_When_BodyInvalid()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var response = await SendAsync(client, HttpMethod.Post, RepaymentsPath(Guid.NewGuid()), Body(0, "x", "XDR"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        errors.EnumerateObject().Select(property => property.Name).ShouldBe(["amount", "currency", "date"], ignoreOrder: true);
    }

    [Fact]
    public async Task Should_Return400_When_BodyMissing()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var response = await client.Http.SendAsync(
            client.WithXsrf(new HttpRequestMessage(HttpMethod.Post, RepaymentsPath(Guid.NewGuid()))),
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_Return404_When_IdIsNotGuid()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var list = await client.Http.GetAsync($"{DebtsPath}/not-a-guid/repayments", Ct);
        using var add = await SendAsync(client, HttpMethod.Post, $"{DebtsPath}/not-a-guid/repayments", Body(1_000, IsoDay(Today)));
        using var delete = await SendAsync(client, HttpMethod.Delete, $"{RepaymentsPath(Guid.NewGuid())}/not-a-guid", null);

        list.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        add.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        delete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_RefuseArchivedAccount_ForRepayment()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var archived = await TestData.AddAccountAsync(factory, userId, archived: true);
        var debt = await TestData.AddDebtAsync(factory, userId, DebtDirection.Lent, 5_000, date: IsoDay(Today));

        using var response = await SendAsync(client, HttpMethod.Post, RepaymentsPath(debt.Id), Body(1_000, IsoDay(Today), accountId: archived.Id));

        await ShouldHaveErrorAsync(response, "accountId", "validation.archivedAccount");
        (await RepaymentCountAsync(factory, debt.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task Should_RefuseAccount_When_UnknownOrForeign()
    {
        using var client = new AuthClient(factory);
        using var other = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        await other.RegisterNewAsync(Ct);
        var debt = await TestData.AddDebtAsync(factory, await UserIdAsync(client), DebtDirection.Lent, 5_000, date: IsoDay(Today));
        var foreign = await TestData.AddAccountAsync(factory, await UserIdAsync(other));

        using var foreignResponse = await SendAsync(client, HttpMethod.Post, RepaymentsPath(debt.Id), Body(1_000, IsoDay(Today), accountId: foreign.Id));
        using var unknownResponse = await SendAsync(client, HttpMethod.Post, RepaymentsPath(debt.Id), Body(1_000, IsoDay(Today), accountId: Guid.NewGuid()));

        await ShouldHaveErrorAsync(foreignResponse, "accountId", "validation.unknownAccount");
        await ShouldHaveErrorAsync(unknownResponse, "accountId", "validation.unknownAccount");
    }

    private static string RepaymentsPath(Guid debtId) => $"{DebtsPath}/{debtId}/repayments";

    private static string IsoDay(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static Dictionary<string, object?> Body(
        long amount,
        string date,
        string currency = "PLN",
        Guid? accountId = null,
        double? rate = null,
        string? note = null) =>
        new()
        {
            ["date"] = date,
            ["amount"] = amount,
            ["currency"] = currency,
            ["rate"] = rate,
            ["accountId"] = accountId,
            ["note"] = note
        };

    private static async Task<(Account Account, Debt Debt)> LendAsync(WebApplicationFactory<Program> app, AuthClient client)
    {
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(app, userId, openingBalance: 100_000);
        var debt = await TestData.AddDebtAsync(app, userId, DebtDirection.Lent, 50_000, account: account, date: IsoDay(Today));
        return (account, debt);
    }

    private static Task<HttpResponseMessage> SendAsync(AuthClient client, HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.Http.SendAsync(client.WithXsrf(request), Ct);
    }

    private static async Task<JsonElement> SingleRepaymentAsync(AuthClient client, Guid debtId)
    {
        using var response = await client.Http.GetAsync(RepaymentsPath(debtId), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return page.GetProperty("items").EnumerateArray().Single();
    }

    private static async Task<JsonElement> DebtPageAsync(AuthClient client, string query)
    {
        using var response = await client.Http.GetAsync($"{DebtsPath}{query}", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task<Dictionary<Guid, long>> BalancesAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync("/v1/accounts", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return page.GetProperty("items").EnumerateArray().ToDictionary(
            item => item.GetProperty("id").GetGuid(),
            item => item.GetProperty("balance").GetInt64());
    }

    private static async Task<int> RepaymentCountAsync(WebApplicationFactory<Program> app, Guid debtId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.DebtRepayments.CountAsync(repayment => repayment.DebtId == debtId, Ct);
    }

    private static async Task<long> RepaidAsync(WebApplicationFactory<Program> app, Guid debtId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Debts.AsNoTracking().Where(debt => debt.Id == debtId).Select(debt => debt.Repaid).SingleAsync(Ct);
    }

    private static void ShouldHaveSummary(JsonElement summary, long owedToMe, long owedByMe, int open, int overdue)
    {
        summary.GetProperty("owedToMe").GetInt64().ShouldBe(owedToMe);
        summary.GetProperty("owedByMe").GetInt64().ShouldBe(owedByMe);
        summary.GetProperty("open").GetInt32().ShouldBe(open);
        summary.GetProperty("overdue").GetInt32().ShouldBe(overdue);
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
