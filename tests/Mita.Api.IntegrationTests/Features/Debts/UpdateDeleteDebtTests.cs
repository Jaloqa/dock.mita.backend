using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Debts;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Debts;

public sealed class UpdateDeleteDebtTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string DebtsPath = "/v1/debts";
    private const string AccountsPath = "/v1/accounts";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Should_Return401_When_NotAuthenticated()
    {
        using var client = new AuthClient(factory);
        var path = $"{DebtsPath}/{Guid.NewGuid()}";

        using var update = await client.Http.PutAsJsonAsync(path, new { }, Ct);
        using var delete = await client.Http.DeleteAsync(path, Ct);

        update.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        delete.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_UpdateDebt_And_MoveLeg_When_AccountChanges()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var source = await TestData.AddAccountAsync(factory, userId, openingBalance: 100_000);
        var target = await TestData.AddAccountAsync(factory, userId);
        var debt = await TestData.AddDebtAsync(
            factory,
            userId,
            DebtDirection.Lent,
            30_000,
            account: source,
            date: IsoDay(Today.AddDays(-3)),
            dueDate: IsoDay(Today.AddDays(5)));
        var before = await BalancesAsync(client);
        before[source.Id].ShouldBe(70_000);
        before[target.Id].ShouldBe(0);

        using var moved = await PutAsync(
            client,
            debt.Id,
            Body("lent", "  Anna ", 30_000, "PLN", IsoDay(Today), accountId: target.Id, note: "  Back by Friday "));

        moved.StatusCode.ShouldBe(HttpStatusCode.OK);
        moved.Headers.Location.ShouldBeNull();
        var body = await moved.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.EnumerateObject().Select(property => property.Name).ShouldBe(
            [
                "id", "direction", "counterparty", "amount", "currency", "repaid", "remaining", "status", "overdue", "date",
                "dueDate", "accountId", "accountAmount", "rate", "note", "repayments"
            ],
            ignoreOrder: true);
        body.GetProperty("id").GetGuid().ShouldBe(debt.Id);
        body.GetProperty("direction").GetString().ShouldBe("lent");
        body.GetProperty("counterparty").GetString().ShouldBe("Anna");
        body.GetProperty("amount").GetInt64().ShouldBe(30_000);
        body.GetProperty("currency").GetString().ShouldBe("PLN");
        body.GetProperty("repaid").GetInt64().ShouldBe(0);
        body.GetProperty("remaining").GetInt64().ShouldBe(30_000);
        body.GetProperty("status").GetString().ShouldBe("open");
        body.GetProperty("overdue").GetBoolean().ShouldBeFalse();
        body.GetProperty("date").GetString().ShouldBe(IsoDay(Today));
        body.GetProperty("dueDate").ValueKind.ShouldBe(JsonValueKind.Null);
        body.GetProperty("accountId").GetGuid().ShouldBe(target.Id);
        body.GetProperty("accountAmount").GetInt64().ShouldBe(30_000);
        body.GetProperty("rate").GetDouble().ShouldBe(1);
        body.GetProperty("note").GetString().ShouldBe("Back by Friday");
        body.GetProperty("repayments").GetInt32().ShouldBe(0);
        var afterMove = await BalancesAsync(client);
        afterMove[source.Id].ShouldBe(100_000);
        afterMove[target.Id].ShouldBe(-30_000);

        using var detached = await PutAsync(client, debt.Id, Body("lent", "Anna", 30_000, "PLN", IsoDay(Today), rate: 7));

        detached.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detachedBody = await detached.Content.ReadFromJsonAsync<JsonElement>(Ct);
        detachedBody.GetProperty("accountId").ValueKind.ShouldBe(JsonValueKind.Null);
        detachedBody.GetProperty("accountAmount").ValueKind.ShouldBe(JsonValueKind.Null);
        detachedBody.GetProperty("rate").ValueKind.ShouldBe(JsonValueKind.Null);
        detachedBody.GetProperty("note").ValueKind.ShouldBe(JsonValueKind.Null);
        var afterDetach = await BalancesAsync(client);
        afterDetach[source.Id].ShouldBe(100_000);
        afterDetach[target.Id].ShouldBe(0);
        (await ListAsync(client, DebtsPath))[debt.Id].GetProperty("counterparty").GetString().ShouldBe("Anna");
    }

    [Fact]
    public async Task Should_RepriceAccountAmount_When_AmountOrRateChanges()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.EUR, IsoDay(Today), 4.2563m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(isolated, userId, openingBalance: 100_000);
        var debt = await TestData.AddDebtAsync(
            isolated,
            userId,
            DebtDirection.Lent,
            5_000,
            Currency.EUR,
            account,
            IsoDay(Today));
        (await BalancesAsync(client))[account.Id].ShouldBe(90_000);

        using var automatic = await PutAsync(client, debt.Id, Body("lent", "Max", 5_000, "EUR", IsoDay(Today), accountId: account.Id));

        automatic.StatusCode.ShouldBe(HttpStatusCode.OK);
        var automaticBody = await automatic.Content.ReadFromJsonAsync<JsonElement>(Ct);
        automaticBody.GetProperty("rate").GetDouble().ShouldBe(4.2563);
        automaticBody.GetProperty("accountAmount").GetInt64().ShouldBe(21_282);
        (await BalancesAsync(client))[account.Id].ShouldBe(78_718);

        using var overridden = await PutAsync(
            client,
            debt.Id,
            Body("lent", "Max", 6_000, "EUR", IsoDay(Today), accountId: account.Id, rate: 4.5));

        overridden.StatusCode.ShouldBe(HttpStatusCode.OK);
        var overriddenBody = await overridden.Content.ReadFromJsonAsync<JsonElement>(Ct);
        overriddenBody.GetProperty("rate").GetDouble().ShouldBe(4.5);
        overriddenBody.GetProperty("accountAmount").GetInt64().ShouldBe(27_000);
        overriddenBody.GetProperty("amount").GetInt64().ShouldBe(6_000);
        (await BalancesAsync(client))[account.Id].ShouldBe(73_000);
    }

    [Fact]
    public async Task Should_KeepArchivedAccount_When_Updating()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var kept = await TestData.AddAccountAsync(factory, userId, openingBalance: 10_000);
        var otherArchived = await TestData.AddAccountAsync(factory, userId, archived: true);
        var debt = await TestData.AddDebtAsync(factory, userId, DebtDirection.Lent, 3_000, account: kept, date: IsoDay(Today));
        await ArchiveAsync(factory, kept.Id);

        using var same = await PutAsync(client, debt.Id, Body("lent", "Max", 4_000, "PLN", IsoDay(Today), accountId: kept.Id));
        using var other = await PutAsync(client, debt.Id, Body("lent", "Max", 4_000, "PLN", IsoDay(Today), accountId: otherArchived.Id));

        same.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await same.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("accountId").GetGuid().ShouldBe(kept.Id);
        body.GetProperty("accountAmount").GetInt64().ShouldBe(4_000);
        await ShouldHaveErrorAsync(other, "accountId", "validation.archivedAccount");
        (await StoredDebtAsync(factory, debt.Id)).AccountId.ShouldBe(kept.Id);
    }

    [Fact]
    public async Task Should_RefuseAccount_When_UnknownOrForeign()
    {
        using var owner = new AuthClient(factory);
        using var intruder = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        await intruder.RegisterNewAsync(Ct);
        var debt = await TestData.AddDebtAsync(factory, await UserIdAsync(owner), DebtDirection.Lent, 3_000);
        var foreign = await TestData.AddAccountAsync(factory, await UserIdAsync(intruder));

        using var foreignResponse = await PutAsync(owner, debt.Id, Body("lent", "Max", 3_000, "PLN", IsoDay(Today), accountId: foreign.Id));
        using var unknownResponse = await PutAsync(owner, debt.Id, Body("lent", "Max", 3_000, "PLN", IsoDay(Today), accountId: Guid.NewGuid()));

        await ShouldHaveErrorAsync(foreignResponse, "accountId", "validation.unknownAccount");
        await ShouldHaveErrorAsync(unknownResponse, "accountId", "validation.unknownAccount");
        (await StoredDebtAsync(factory, debt.Id)).AccountId.ShouldBeNull();
    }

    [Fact]
    public async Task Should_RefuseCurrencyChange_When_RepaymentsExist()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var debt = await TestData.AddDebtAsync(factory, userId, DebtDirection.Lent, 30_000);
        var repayment = await TestData.AddRepaymentAsync(factory, debt, 10_000);

        using var refused = await PutAsync(client, debt.Id, Body("lent", "Max", 30_000, "EUR", IsoDay(Today)));

        await ShouldHaveErrorAsync(refused, "currency", "validation.debtCurrencyLocked");
        (await StoredDebtAsync(factory, debt.Id)).Currency.ShouldBe(Currency.PLN);

        using var removed = await DeleteAsync(client, $"{DebtsPath}/{debt.Id}/repayments/{repayment.Id}");
        removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var allowed = await PutAsync(client, debt.Id, Body("lent", "Max", 30_000, "EUR", IsoDay(Today)));

        allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await allowed.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("currency").GetString().ShouldBe("EUR");
    }

    [Fact]
    public async Task Should_AllowSameCurrency_When_RepaymentsExist()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var debt = await TestData.AddDebtAsync(factory, await UserIdAsync(client), DebtDirection.Lent, 30_000);
        await TestData.AddRepaymentAsync(factory, debt, 10_000);

        using var response = await PutAsync(client, debt.Id, Body("lent", "Anna", 40_000, "PLN", IsoDay(Today)));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("amount").GetInt64().ShouldBe(40_000);
        body.GetProperty("repaid").GetInt64().ShouldBe(10_000);
        body.GetProperty("remaining").GetInt64().ShouldBe(30_000);
        body.GetProperty("repayments").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Should_RefuseAmountBelowRepaid()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var debt = await TestData.AddDebtAsync(factory, await UserIdAsync(client), DebtDirection.Lent, 30_000);
        await TestData.AddRepaymentAsync(factory, debt, 10_000);

        using var refused = await PutAsync(client, debt.Id, Body("lent", "Max", 9_999, "PLN", IsoDay(Today)));

        await ShouldHaveErrorAsync(refused, "amount", "validation.belowRepaid");
        var unchanged = await StoredDebtAsync(factory, debt.Id);
        unchanged.Amount.ShouldBe(30_000);
        unchanged.Repaid.ShouldBe(10_000);

        using var settled = await PutAsync(client, debt.Id, Body("lent", "Max", 10_000, "PLN", IsoDay(Today)));

        settled.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await settled.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("amount").GetInt64().ShouldBe(10_000);
        body.GetProperty("repaid").GetInt64().ShouldBe(10_000);
        body.GetProperty("remaining").GetInt64().ShouldBe(0);
        body.GetProperty("status").GetString().ShouldBe("settled");
    }

    [Fact]
    public async Task Should_ReportBoth_When_CurrencyLockedAndAmountBelowRepaid()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var debt = await TestData.AddDebtAsync(factory, await UserIdAsync(client), DebtDirection.Lent, 30_000);
        await TestData.AddRepaymentAsync(factory, debt, 10_000);

        using var refused = await PutAsync(client, debt.Id, Body("lent", "Max", 9_999, "EUR", IsoDay(Today)));

        await ShouldHaveErrorsAsync(
            refused,
            ("amount", "validation.belowRepaid"),
            ("currency", "validation.debtCurrencyLocked"));
        var unchanged = await StoredDebtAsync(factory, debt.Id);
        unchanged.Amount.ShouldBe(30_000);
        unchanged.Currency.ShouldBe(Currency.PLN);
    }

    [Fact]
    public async Task Should_AllowDirectionChange_When_RepaymentsExist()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(factory, userId, openingBalance: 100_000);
        var debt = await TestData.AddDebtAsync(factory, userId, DebtDirection.Lent, 30_000, account: account, date: IsoDay(Today));
        await TestData.AddRepaymentAsync(factory, debt, 10_000, account, IsoDay(Today));
        (await BalancesAsync(client))[account.Id].ShouldBe(80_000);

        using var response = await PutAsync(client, debt.Id, Body("borrowed", "Max", 30_000, "PLN", IsoDay(Today), accountId: account.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("direction").GetString().ShouldBe("borrowed");
        body.GetProperty("repaid").GetInt64().ShouldBe(10_000);
        (await BalancesAsync(client))[account.Id].ShouldBe(120_000);
    }

    [Fact]
    public async Task Should_DeleteDebt_WithRepayments_And_RestoreBalances()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var source = await TestData.AddAccountAsync(factory, userId, openingBalance: 100_000);
        var target = await TestData.AddAccountAsync(factory, userId, openingBalance: 5_000);
        var debt = await TestData.AddDebtAsync(factory, userId, DebtDirection.Lent, 30_000, account: source, date: IsoDay(Today));
        await TestData.AddRepaymentAsync(factory, debt, 10_000, target, IsoDay(Today));
        var before = await BalancesAsync(client);
        before[source.Id].ShouldBe(70_000);
        before[target.Id].ShouldBe(15_000);
        using var sourceInUse = await DeleteAsync(client, $"{AccountsPath}/{source.Id}");
        using var targetInUse = await DeleteAsync(client, $"{AccountsPath}/{target.Id}");
        await ShouldHaveErrorAsync(sourceInUse, "id", "validation.accountInUse");
        await ShouldHaveErrorAsync(targetInUse, "id", "validation.accountInUse");

        using var deleted = await DeleteAsync(client, $"{DebtsPath}/{debt.Id}");

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var after = await BalancesAsync(client);
        after[source.Id].ShouldBe(100_000);
        after[target.Id].ShouldBe(5_000);
        (await ListAsync(client, DebtsPath)).ContainsKey(debt.Id).ShouldBeFalse();
        using var repayments = await client.Http.GetAsync($"{DebtsPath}/{debt.Id}/repayments", Ct);
        repayments.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await RepaymentCountAsync(factory, debt.Id)).ShouldBe(0);
        using var sourceDeleted = await DeleteAsync(client, $"{AccountsPath}/{source.Id}");
        using var targetDeleted = await DeleteAsync(client, $"{AccountsPath}/{target.Id}");
        sourceDeleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        targetDeleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Should_Return404_When_DebtUnknownOrForeign()
    {
        using var owner = new AuthClient(factory);
        using var intruder = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        await intruder.RegisterNewAsync(Ct);
        var ownerId = await UserIdAsync(owner);
        var account = await TestData.AddAccountAsync(factory, ownerId, openingBalance: 10_000);
        var debt = await TestData.AddDebtAsync(factory, ownerId, DebtDirection.Lent, 3_000, account: account, date: IsoDay(Today));
        var unknown = Guid.NewGuid();
        var valid = Body("borrowed", "Eve", 1_000, "PLN", IsoDay(Today));

        using var unknownUpdate = await PutAsync(owner, unknown, valid);
        using var unknownDelete = await DeleteAsync(owner, $"{DebtsPath}/{unknown}");
        using var foreignUpdate = await PutAsync(intruder, debt.Id, valid);
        using var foreignDelete = await DeleteAsync(intruder, $"{DebtsPath}/{debt.Id}");
        using var notAGuid = await PutAsync(owner, "not-a-guid", valid);
        using var invalidBody = await PutAsync(owner, unknown, Body("lent", "Max", 0, "PLN", IsoDay(Today)));

        unknownUpdate.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        unknownDelete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        foreignUpdate.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        foreignDelete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        notAGuid.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ShouldHaveErrorAsync(invalidBody, "amount", "validation.positiveAmount");
        var stored = await StoredDebtAsync(factory, debt.Id);
        stored.Direction.ShouldBe(DebtDirection.Lent);
        stored.Counterparty.ShouldBe("Max");
        stored.Amount.ShouldBe(3_000);
        (await BalancesAsync(owner))[account.Id].ShouldBe(7_000);
        (await ListAsync(owner, DebtsPath)).ContainsKey(debt.Id).ShouldBeTrue();
    }

    private static string IsoDay(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

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

    private static Task<HttpResponseMessage> PutAsync(AuthClient client, Guid id, object body) =>
        PutAsync(client, id.ToString(), body);

    private static Task<HttpResponseMessage> PutAsync(AuthClient client, string id, object body) =>
        client.Http.SendAsync(
            client.WithXsrf(new HttpRequestMessage(HttpMethod.Put, $"{DebtsPath}/{id}") { Content = JsonContent.Create(body) }),
            Ct);

    private static Task<HttpResponseMessage> DeleteAsync(AuthClient client, string path) =>
        client.Http.SendAsync(client.WithXsrf(new HttpRequestMessage(HttpMethod.Delete, path)), Ct);

    private static async Task<Dictionary<Guid, JsonElement>> ListAsync(AuthClient client, string path)
    {
        using var response = await client.Http.GetAsync(path, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return page.GetProperty("items").EnumerateArray().ToDictionary(item => item.GetProperty("id").GetGuid());
    }

    private static async Task<Dictionary<Guid, long>> BalancesAsync(AuthClient client)
    {
        var accounts = await ListAsync(client, AccountsPath);
        return accounts.ToDictionary(pair => pair.Key, pair => pair.Value.GetProperty("balance").GetInt64());
    }

    private static async Task<Debt> StoredDebtAsync(WebApplicationFactory<Program> app, Guid debtId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Debts.AsNoTracking().SingleAsync(debt => debt.Id == debtId, Ct);
    }

    private static async Task<int> RepaymentCountAsync(WebApplicationFactory<Program> app, Guid debtId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.DebtRepayments.CountAsync(repayment => repayment.DebtId == debtId, Ct);
    }

    private static async Task ArchiveAsync(WebApplicationFactory<Program> app, Guid accountId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var account = await db.Accounts.SingleAsync(row => row.Id == accountId, Ct);
        account.Update(account.Name, account.Type, account.Currency, account.OpeningBalance, account.CardLast4, archived: true);
        await db.SaveChangesAsync(Ct);
    }

    private static Task ShouldHaveErrorAsync(HttpResponseMessage response, string field, string code) =>
        ShouldHaveErrorsAsync(response, (field, code));

    private static async Task ShouldHaveErrorsAsync(HttpResponseMessage response, params (string Field, string Code)[] expected)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        errors.EnumerateObject().Select(property => property.Name).ShouldBe(
            expected.Select(error => error.Field),
            ignoreOrder: true);
        foreach (var (field, code) in expected)
        {
            errors.GetProperty(field).EnumerateArray().Select(error => error.GetString()).ShouldBe([code]);
        }
    }

    private static async Task<Guid> UserIdAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync("/v1/me", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }
}
