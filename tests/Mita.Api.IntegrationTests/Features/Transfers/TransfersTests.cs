using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Money;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Transfers;

public sealed class TransfersTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string TransfersPath = "/v1/transfers";
    private const string AccountsPath = "/v1/accounts";
    private const string TransferDate = "2026-03-02";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_Return401_When_NotAuthenticated()
    {
        using var client = new AuthClient(factory);

        using var response = await client.Http.GetAsync(TransfersPath, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_CreateTransfer_When_SameCurrency()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var from = await CreateAccountAsync(client, "PLN", openingBalance: 10_000);
        var to = await CreateAccountAsync(client, "PLN", openingBalance: 500);

        using var response = await PostAsync(client, Body(from, to, 2500, rate: 3.5, note: "  Rent  "));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldBeNull();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.EnumerateObject().Select(property => property.Name).ShouldBe(
            ["id", "date", "fromAccountId", "toAccountId", "fromAmount", "toAmount", "rate", "note"],
            ignoreOrder: true);
        body.GetProperty("date").GetString().ShouldBe(TransferDate);
        body.GetProperty("fromAccountId").GetGuid().ShouldBe(from);
        body.GetProperty("toAccountId").GetGuid().ShouldBe(to);
        body.GetProperty("fromAmount").GetInt64().ShouldBe(2500);
        body.GetProperty("toAmount").GetInt64().ShouldBe(2500);
        body.GetProperty("rate").GetDouble().ShouldBe(1);
        body.GetProperty("note").GetString().ShouldBe("Rent");

        using var blankNote = await PostAsync(client, Body(from, to, 100, note: "   "));
        blankNote.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await blankNote.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("note").ValueKind
            .ShouldBe(JsonValueKind.Null);

        var accounts = await ListAccountsAsync(client);
        accounts[from].GetProperty("balance").GetInt64().ShouldBe(7400);
        accounts[from].GetProperty("inUse").GetBoolean().ShouldBeTrue();
        accounts[to].GetProperty("balance").GetInt64().ShouldBe(3100);
        accounts[to].GetProperty("inUse").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Should_UseStoredRate_When_CurrenciesDiffer()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.EUR, TransferDate, 4.2717m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var from = await CreateAccountAsync(client, "EUR");
        var to = await CreateAccountAsync(client, "PLN");

        using var response = await PostAsync(client, Body(from, to, 10_000));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("rate").GetDouble().ShouldBe(4.2717);
        body.GetProperty("toAmount").GetInt64().ShouldBe(42_717);
    }

    [Fact]
    public async Task Should_UseManualRate_When_Given()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var from = await CreateAccountAsync(client, "EUR");
        var to = await CreateAccountAsync(client, "PLN");

        using var response = await PostAsync(client, Body(from, to, 10_000, rate: 4.5));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("rate").GetDouble().ShouldBe(4.5);
        body.GetProperty("toAmount").GetInt64().ShouldBe(45_000);
    }

    [Fact]
    public async Task Should_ReturnPositiveAmount_When_ConvertedAmountIsZero()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var from = await CreateAccountAsync(client, "EUR");
        var to = await CreateAccountAsync(client, "PLN");

        using var response = await PostAsync(client, Body(from, to, 10_000, rate: 1e-9));

        await ShouldHaveFieldErrorsAsync(response, ("rate", "validation.positiveAmount"));
        (await ListTransfersAsync(client)).GetProperty("total").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Should_RejectTransfer_When_ConvertedAmountExceedsMax()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var from = await CreateAccountAsync(client, "PLN");
        var to = await CreateAccountAsync(client, "EUR");

        using var maxRate = await PostAsync(client, Body(from, to, MinorUnits.MaxAmount, rate: 1_000_000));
        using var justAbove = await PostAsync(client, Body(from, to, 50_000_000_001, rate: 2));

        await ShouldHaveFieldErrorsAsync(maxRate, ("rate", "validation.invalidValue"));
        await ShouldHaveFieldErrorsAsync(justAbove, ("rate", "validation.invalidValue"));
        (await ListTransfersAsync(client)).GetProperty("total").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Should_StoreConvertedAmount_When_ConversionLandsOnMax()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var from = await CreateAccountAsync(client, "PLN");
        var to = await CreateAccountAsync(client, "EUR");

        using var response = await PostAsync(client, Body(from, to, 50_000_000_000, rate: 2));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("toAmount").GetInt64().ShouldBe(MinorUnits.MaxAmount);
        body.GetProperty("rate").GetDouble().ShouldBe(2);
    }

    [Fact]
    public async Task Should_Return400_When_ManualRateHuge()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var from = await CreateAccountAsync(client, "PLN");
        var to = await CreateAccountAsync(client, "EUR");
        var json = $$"""{"date":"{{TransferDate}}","fromAccountId":"{{from}}","toAccountId":"{{to}}","fromAmount":100,"rate":1e300}""";
        var request = client.WithXsrf(new HttpRequestMessage(HttpMethod.Post, TransfersPath)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });

        using var response = await client.Http.SendAsync(request, Ct);

        await ShouldHaveFieldErrorsAsync(response, ("rate", "validation.invalidValue"));
    }

    [Fact]
    public async Task Should_Return503_When_NoRatesAndNoManualRate()
    {
        await using var isolated = factory.WithIsolatedData();
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var from = await CreateAccountAsync(client, "EUR");
        var to = await CreateAccountAsync(client, "PLN");

        using var response = await PostAsync(client, Body(from, to, 10_000));

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("title").GetString().ShouldBe("Service Unavailable");
        problem.GetProperty("code").GetString().ShouldBe("rates.unavailable");
    }

    [Fact]
    public async Task Should_Return503_When_RateIsStale()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.KWD, "2026-01-21", 12.6221m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var from = await CreateAccountAsync(client, "KWD");
        var to = await CreateAccountAsync(client, "PLN");

        using var automatic = await PostAsync(client, Body(from, to, 1_000));
        using var manual = await PostAsync(client, Body(from, to, 1_000, rate: 12.5));

        automatic.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var problem = await automatic.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("code").GetString().ShouldBe("rates.unavailable");
        manual.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Should_ReturnFieldErrors_When_AccountsInvalid()
    {
        using var owner = new AuthClient(factory);
        using var other = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        await other.RegisterNewAsync(Ct);
        var own = await CreateAccountAsync(owner, "PLN");
        var archived = await CreateAccountAsync(owner, "PLN", archived: true);
        var foreign = await CreateAccountAsync(other, "PLN");

        using var foreignResponse = await PostAsync(owner, Body(foreign, own, 100));
        using var archivedResponse = await PostAsync(owner, Body(own, archived, 100));
        using var sameResponse = await PostAsync(owner, Body(own, own, 100));
        using var unknownResponse = await PostAsync(owner, Body(Guid.NewGuid(), archived, 100));

        await ShouldHaveFieldErrorsAsync(foreignResponse, ("fromAccountId", "validation.unknownAccount"));
        await ShouldHaveFieldErrorsAsync(archivedResponse, ("toAccountId", "validation.archivedAccount"));
        await ShouldHaveFieldErrorsAsync(sameResponse, ("toAccountId", "validation.sameAccount"));
        await ShouldHaveFieldErrorsAsync(
            unknownResponse,
            ("fromAccountId", "validation.unknownAccount"),
            ("toAccountId", "validation.archivedAccount"));
        (await ListTransfersAsync(owner)).GetProperty("total").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Should_RejectTransfer_When_AccountArchivedConcurrently()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var from = await CreateAccountAsync(client, "PLN", openingBalance: 1000);
            var to = await CreateAccountAsync(client, "PLN");

            var archive = PutAccountAsync(client, from, AccountBody("PLN", openingBalance: 1000, archived: true));
            var transfer = PostAsync(client, Body(from, to, 100));
            await Task.WhenAll(archive, transfer);
            using var archiveResponse = await archive;
            using var transferResponse = await transfer;

            archiveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            transferResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.Created, HttpStatusCode.BadRequest);
            if (transferResponse.StatusCode == HttpStatusCode.BadRequest)
            {
                await ShouldHaveFieldErrorsAsync(transferResponse, ("fromAccountId", "validation.archivedAccount"));
            }
        }
    }

    [Fact]
    public async Task Should_ListNewestFirst_And_FilterByAccount()
    {
        using var client = new AuthClient(factory);
        using var other = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        await other.RegisterNewAsync(Ct);
        var a = await CreateAccountAsync(client, "PLN", openingBalance: 10_000);
        var b = await CreateAccountAsync(client, "PLN");
        var c = await CreateAccountAsync(client, "PLN");
        var first = await CreateTransferAsync(client, Body(a, b, 100, date: "2026-03-01"));
        var newest = await CreateTransferAsync(client, Body(b, c, 50, date: "2026-03-05"));
        var later = await CreateTransferAsync(client, Body(a, c, 25, date: "2026-03-01"));

        var all = await ListTransfersAsync(client);
        var byB = await ListTransfersAsync(client, $"accountId={b}");
        var foreign = await ListTransfersAsync(other);
        var foreignByB = await ListTransfersAsync(other, $"accountId={b}");

        Ids(all).ShouldBe([newest, later, first]);
        all.GetProperty("total").GetInt32().ShouldBe(3);
        all.GetProperty("page").GetInt32().ShouldBe(1);
        all.GetProperty("pageSize").GetInt32().ShouldBe(20);
        Ids(byB).ShouldBe([newest, first]);
        byB.GetProperty("total").GetInt32().ShouldBe(2);
        foreign.GetProperty("total").GetInt32().ShouldBe(0);
        foreignByB.GetProperty("total").GetInt32().ShouldBe(0);

        var paged = await ListTransfersAsync(client, "page=2&pageSize=2");
        Ids(paged).ShouldBe([first]);
    }

    [Fact]
    public async Task Should_ShowNewTransfer_When_ListWasCached()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var from = await CreateAccountAsync(client, "PLN");
        var to = await CreateAccountAsync(client, "PLN");
        (await ListTransfersAsync(client)).GetProperty("total").GetInt32().ShouldBe(0);
        (await ListAccountsAsync(client))[from].GetProperty("inUse").GetBoolean().ShouldBeFalse();

        await CreateTransferAsync(client, Body(from, to, 100));

        (await ListTransfersAsync(client)).GetProperty("total").GetInt32().ShouldBe(1);
        (await ListAccountsAsync(client))[from].GetProperty("inUse").GetBoolean().ShouldBeTrue();
    }

    [Theory]
    [InlineData("accountId=abc", "accountId")]
    [InlineData("pageSize=101", "pageSize")]
    [InlineData("page=0", "page")]
    public async Task Should_Return400_When_QueryValueInvalid(string query, string parameter)
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var response = await client.Http.GetAsync($"{TransfersPath}?{query}", Ct);

        await ShouldHaveFieldErrorsAsync(response, (parameter, "validation.invalidValue"));
    }

    private static Dictionary<string, object?> Body(
        Guid from,
        Guid to,
        long amount,
        double? rate = null,
        string? note = null,
        string date = TransferDate)
    {
        var body = new Dictionary<string, object?>
        {
            ["date"] = date,
            ["fromAccountId"] = from,
            ["toAccountId"] = to,
            ["fromAmount"] = amount
        };
        if (rate is not null)
        {
            body["rate"] = rate;
        }

        if (note is not null)
        {
            body["note"] = note;
        }

        return body;
    }

    private static Dictionary<string, object?> AccountBody(string currency, long openingBalance = 0, bool archived = false) => new()
    {
        ["name"] = "Account",
        ["type"] = "cash",
        ["currency"] = currency,
        ["openingBalance"] = openingBalance,
        ["archived"] = archived
    };

    private static async Task<Guid> CreateAccountAsync(
        AuthClient client,
        string currency,
        long openingBalance = 0,
        bool archived = false)
    {
        var request = client.WithXsrf(new HttpRequestMessage(HttpMethod.Post, AccountsPath)
        {
            Content = JsonContent.Create(AccountBody(currency, openingBalance, archived))
        });
        using var response = await client.Http.SendAsync(request, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> PutAccountAsync(AuthClient client, Guid id, object body) =>
        client.Http.SendAsync(
            client.WithXsrf(new HttpRequestMessage(HttpMethod.Put, $"{AccountsPath}/{id}") { Content = JsonContent.Create(body) }),
            Ct);

    private static Task<HttpResponseMessage> PostAsync(AuthClient client, object body) =>
        client.Http.SendAsync(
            client.WithXsrf(new HttpRequestMessage(HttpMethod.Post, TransfersPath) { Content = JsonContent.Create(body) }),
            Ct);

    private static async Task<Guid> CreateTransferAsync(AuthClient client, object body)
    {
        using var response = await PostAsync(client, body);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> ListTransfersAsync(AuthClient client, string? query = null)
    {
        using var response = await client.Http.GetAsync(query is null ? TransfersPath : $"{TransfersPath}?{query}", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task<Dictionary<Guid, JsonElement>> ListAccountsAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync(AccountsPath, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return page.GetProperty("items").EnumerateArray().ToDictionary(item => item.GetProperty("id").GetGuid());
    }

    private static Guid[] Ids(JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];

    private static async Task ShouldHaveFieldErrorsAsync(
        HttpResponseMessage response,
        params (string Field, string Code)[] expected)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var errors = problem.GetProperty("errors").EnumerateObject()
            .Select(property => (property.Name, property.Value.EnumerateArray().Select(code => code.GetString()!).Single()))
            .ToArray();
        errors.ShouldBe(expected, ignoreOrder: true);
    }
}
