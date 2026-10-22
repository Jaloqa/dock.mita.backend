using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Debts;
using Mita.Domain.Money;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Accounts;

public sealed class UpdateDeleteAccountTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string AccountsPath = "/v1/accounts";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_UpdateAllFields_When_InputValid()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var id = await CreateAsync(client, Body());
        (await ListAsync(client)).GetProperty("items")[0].GetProperty("name").GetString().ShouldBe("Main");

        using var response = await PutAsync(
            client,
            id,
            Body(name: "  Wallet ", type: "cash", currency: "USD", openingBalance: 999, cardLast4: "9999", archived: true));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("id").GetGuid().ShouldBe(id);
        body.GetProperty("name").GetString().ShouldBe("Wallet");
        body.GetProperty("type").GetString().ShouldBe("cash");
        body.GetProperty("currency").GetString().ShouldBe("USD");
        body.GetProperty("openingBalance").GetInt64().ShouldBe(999);
        body.GetProperty("balance").GetInt64().ShouldBe(999);
        body.GetProperty("cardLast4").ValueKind.ShouldBe(JsonValueKind.Null);
        body.GetProperty("archived").GetBoolean().ShouldBeTrue();

        var listed = (await ListAsync(client)).GetProperty("items")[0];
        listed.GetProperty("name").GetString().ShouldBe("Wallet");
        listed.GetProperty("archived").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Should_KeepArchived_When_ArchivedOmitted()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var id = await CreateAsync(client, Body(archived: true));
        var update = Body(name: "Renamed");
        update.Remove("archived");

        using var response = await PutAsync(client, id, update);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("name").GetString().ShouldBe("Renamed");
        body.GetProperty("archived").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Should_ArchiveAndRestore()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var id = await CreateAsync(client, Body());

        using var archived = await PutAsync(client, id, Body(archived: true));
        using var restored = await PutAsync(client, id, Body(archived: false));

        (await archived.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("archived").GetBoolean().ShouldBeTrue();
        (await restored.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("archived").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Should_ChangeCurrency_When_AccountNotInUse()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var id = await CreateAsync(client, Body(currency: "EUR"));

        using var response = await PutAsync(client, id, Body(currency: "UAH"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("currency").GetString().ShouldBe("UAH");
    }

    [Fact]
    public async Task Should_ReturnCurrencyLocked_When_TransferExists()
    {
        using var client = new AuthClient(factory);
        var (from, _) = await SeedTransferAsync(client);

        using var response = await PutAsync(client, from, Body(currency: "USD"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var errors = problem.GetProperty("errors");
        errors.EnumerateObject().Select(property => property.Name).ShouldBe(["currency"]);
        errors.GetProperty("currency").EnumerateArray().Select(code => code.GetString())
            .ShouldBe(["validation.currencyLocked"]);
    }

    [Fact]
    public async Task Should_AllowSameCurrency_When_TransferExists()
    {
        using var client = new AuthClient(factory);
        var (from, _) = await SeedTransferAsync(client);

        using var response = await PutAsync(client, from, Body(name: "Renamed", currency: "PLN"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("name").GetString().ShouldBe("Renamed");
        body.GetProperty("inUse").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Return404_When_UpdatingUnknownAccount()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var response = await PutAsync(client, Guid.NewGuid(), Body());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_Return400_When_UpdateBodyInvalid()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var id = await CreateAsync(client, Body());

        using var response = await PutAsync(client, id, Body(name: "   "));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("errors").GetProperty("name")[0].GetString().ShouldBe("validation.required");
    }

    [Fact]
    public async Task Should_Return404_When_AccountBelongsToAnotherUser()
    {
        using var owner = new AuthClient(factory);
        using var other = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        await other.RegisterNewAsync(Ct);
        var id = await CreateAsync(owner, Body());

        using var put = await PutAsync(other, id, Body(name: "Hijack"));
        using var delete = await DeleteAsync(other, id);

        put.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        delete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ListAsync(owner)).GetProperty("items")[0].GetProperty("name").GetString().ShouldBe("Main");
    }

    [Fact]
    public async Task Should_Return404_When_ForeignAccountIsInUse()
    {
        using var owner = new AuthClient(factory);
        using var other = new AuthClient(factory);
        var (from, to) = await SeedTransferAsync(owner);
        await other.RegisterNewAsync(Ct);

        foreach (var id in new[] { from, to })
        {
            using var put = await PutAsync(other, id, Body(currency: "USD"));
            using var delete = await DeleteAsync(other, id);

            put.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            delete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        (await ListAsync(owner)).GetProperty("total").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task Should_DeleteAccount_When_NotInUse()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var id = await CreateAsync(client, Body());
        (await ListAsync(client)).GetProperty("total").GetInt32().ShouldBe(1);

        using var response = await DeleteAsync(client, id);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ListAsync(client)).GetProperty("total").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Should_Return404_When_DeletingUnknownAccount()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var response = await DeleteAsync(client, Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_ReturnAccountInUse_When_DeletingAccountWithTransfers()
    {
        using var client = new AuthClient(factory);
        var (from, to) = await SeedTransferAsync(client);

        foreach (var id in new[] { from, to })
        {
            using var response = await DeleteAsync(client, id);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            problem.GetProperty("errors").GetProperty("id").EnumerateArray().Select(code => code.GetString())
                .ShouldBe(["validation.accountInUse"]);
        }

        (await ListAsync(client)).GetProperty("total").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task Should_RefuseDelete_When_AccountHasDebtLegs()
    {
        using var client = new AuthClient(factory);
        var (debtAccount, repaymentAccount) = await SeedDebtLegsAsync(client);

        foreach (var id in new[] { debtAccount, repaymentAccount })
        {
            using var response = await DeleteAsync(client, id);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            problem.GetProperty("errors").GetProperty("id").EnumerateArray().Select(code => code.GetString())
                .ShouldBe(["validation.accountInUse"]);
        }

        (await ListAsync(client)).GetProperty("total").GetInt32().ShouldBe(2);

        foreach (var id in new[] { debtAccount, repaymentAccount })
        {
            using var response = await PutAsync(client, id, Body(currency: "USD"));

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            var errors = problem.GetProperty("errors");
            errors.EnumerateObject().Select(property => property.Name).ShouldBe(["currency"]);
            errors.GetProperty("currency").EnumerateArray().Select(code => code.GetString())
                .ShouldBe(["validation.currencyLocked"]);
        }
    }

    private async Task<(Guid DebtAccount, Guid RepaymentAccount)> SeedDebtLegsAsync(AuthClient client)
    {
        var userId = await RegisterAsync(client);
        var debtAccount = await TestData.AddAccountAsync(factory, userId, Currency.PLN, 10_000);
        var repaymentAccount = await TestData.AddAccountAsync(factory, userId, Currency.PLN, 0);
        await TestData.AddDebtAsync(factory, userId, DebtDirection.Lent, 3_000, account: debtAccount);
        var detached = await TestData.AddDebtAsync(factory, userId, DebtDirection.Lent, 999);
        await TestData.AddRepaymentAsync(factory, detached, 500, repaymentAccount);
        return (debtAccount.Id, repaymentAccount.Id);
    }

    private async Task<(Guid From, Guid To)> SeedTransferAsync(AuthClient client)
    {
        var userId = await RegisterAsync(client);
        var from = await TestData.AddAccountAsync(factory, userId, Currency.PLN, 10_000);
        var to = await TestData.AddAccountAsync(factory, userId, Currency.PLN, 0);
        await TestData.AddTransferAsync(factory, from, to, 100);
        return (from.Id, to.Id);
    }

    private static async Task<Guid> RegisterAsync(AuthClient client)
    {
        var credentials = await client.RegisterNewAsync(Ct);
        using var me = await client.Http.GetAsync("/v1/me", Ct);
        var meBody = await me.Content.ReadFromJsonAsync<JsonElement>(Ct);
        meBody.GetProperty("email").GetString().ShouldBe(credentials.Email);
        return meBody.GetProperty("id").GetGuid();
    }

    private static Dictionary<string, object?> Body(
        object? name = null,
        object? type = null,
        object? currency = null,
        object? openingBalance = null,
        object? cardLast4 = null,
        object? archived = null) => new()
    {
        ["name"] = name ?? "Main",
        ["type"] = type ?? "card",
        ["currency"] = currency ?? "EUR",
        ["openingBalance"] = openingBalance ?? 0,
        ["cardLast4"] = cardLast4 ?? (type is null or "card" ? "1234" : null),
        ["archived"] = archived ?? false
    };

    private static async Task<Guid> CreateAsync(AuthClient client, object body)
    {
        var request = client.WithXsrf(new HttpRequestMessage(HttpMethod.Post, AccountsPath) { Content = JsonContent.Create(body) });
        using var response = await client.Http.SendAsync(request, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> PutAsync(AuthClient client, Guid id, object body)
    {
        var request = client.WithXsrf(new HttpRequestMessage(HttpMethod.Put, $"{AccountsPath}/{id}") { Content = JsonContent.Create(body) });
        return client.Http.SendAsync(request, Ct);
    }

    private static Task<HttpResponseMessage> DeleteAsync(AuthClient client, Guid id) =>
        client.Http.SendAsync(client.WithXsrf(new HttpRequestMessage(HttpMethod.Delete, $"{AccountsPath}/{id}")), Ct);

    private static async Task<JsonElement> ListAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync(AccountsPath, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }
}
