using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Money;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Groups;

public sealed class GroupBalancesTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string GroupsPath = "/v1/groups";

    private static readonly string[] BalancesProperties = ["currency", "groupCurrency", "total", "members", "transfers"];

    private static readonly string[] MemberProperties = ["memberId", "paid", "share", "sent", "received", "balance"];

    private static readonly string[] TransferProperties = ["fromMemberId", "toMemberId", "amount"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Should_ComputeBalances_AndTransfers()
    {
        await using var isolated = factory.WithIsolatedData();
        await SeedRatesAsync(isolated);
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var (groupId, members) = await SeedAlbaniaAsync(isolated, client);

        var body = await GetBalancesAsync(client, groupId);

        body.EnumerateObject().Select(property => property.Name).ShouldBe(BalancesProperties);
        body.GetProperty("currency").GetString().ShouldBe("EUR");
        body.GetProperty("groupCurrency").GetString().ShouldBe("EUR");
        body.GetProperty("total").GetInt64().ShouldBe(19_070);
        MemberFigures(body).ShouldBe(
        [
            (members[0], 12_000, 5_768, 0, 2_000, 4_232),
            (members[1], 3_070, 3_768, 0, 0, -698),
            (members[2], 4_000, 5_767, 0, 0, -1_767),
            (members[3], 0, 3_767, 2_000, 0, -1_767)
        ]);
        TransferFigures(body).ShouldBe(
        [
            (members[2], members[0], 1_767),
            (members[3], members[0], 1_767),
            (members[1], members[0], 698)
        ]);
        foreach (var member in body.GetProperty("members").EnumerateArray())
        {
            member.EnumerateObject().Select(property => property.Name).ShouldBe(MemberProperties);
        }

        foreach (var transfer in body.GetProperty("transfers").EnumerateArray())
        {
            transfer.EnumerateObject().Select(property => property.Name).ShouldBe(TransferProperties);
        }

        var same = await GetBalancesAsync(client, groupId, "EUR");
        same.GetRawText().ShouldBe(body.GetRawText());
    }

    [Fact]
    public async Task Should_ViewBalances_InAnotherCurrency()
    {
        await using var isolated = factory.WithIsolatedData();
        await SeedRatesAsync(isolated);
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var (groupId, members) = await SeedAlbaniaAsync(isolated, client);

        var body = await GetBalancesAsync(client, groupId, "PLN");

        body.GetProperty("currency").GetString().ShouldBe("PLN");
        body.GetProperty("groupCurrency").GetString().ShouldBe("EUR");
        body.GetProperty("total").GetInt64().ShouldBe(82_001);
        var figures = MemberFigures(body);
        figures[0].ShouldBe((members[0], 51_600, 24_802, 0, 8_600, 18_198));
        figures[1].Balance.ShouldBe(-3_001);
        TransferFigures(body).ShouldBe(
        [
            (members[2], members[0], 7_598),
            (members[3], members[0], 7_598),
            (members[1], members[0], 3_001)
        ]);
    }

    [Fact]
    public async Task Should_Return503_When_ViewCurrencyHasNoRate()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.EUR, IsoDay(Today), 4.30m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var (group, _) = await TestData.AddGroupAsync(isolated, await UserIdAsync(client));

        using var unavailable = await client.Http.GetAsync($"{GroupsPath}/{group.Id}/balances?currency=KWD", Ct);

        unavailable.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await unavailable.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString()
            .ShouldBe("rates.unavailable");

        foreach (var invalid in new[] { "eur", "", "XDR", " EUR" })
        {
            using var rejected = await client.Http.GetAsync(
                $"{GroupsPath}/{group.Id}/balances?currency={Uri.EscapeDataString(invalid)}",
                Ct);

            rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var errors = (await rejected.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
            errors.EnumerateObject().Select(property => property.Name).ShouldBe(["currency"]);
            errors.GetProperty("currency").EnumerateArray().Select(error => error.GetString())
                .ShouldBe(["validation.invalidValue"]);
        }
    }

    [Fact]
    public async Task Should_ReturnZeroBalances_ForNewGroup()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, members) = await TestData.AddGroupAsync(factory, await UserIdAsync(client));

        var body = await GetBalancesAsync(client, group.Id);

        body.GetProperty("currency").GetString().ShouldBe("EUR");
        body.GetProperty("total").GetInt64().ShouldBe(0);
        MemberFigures(body).ShouldBe(members.Select(member => (member.Id, 0L, 0L, 0L, 0L, 0L)).ToList());
        body.GetProperty("transfers").ValueKind.ShouldBe(JsonValueKind.Array);
        body.GetProperty("transfers").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Should_SumToZero_ForManyEntries()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, rows) = await TestData.AddGroupAsync(
            factory,
            await UserIdAsync(client),
            "Many",
            Currency.EUR,
            "Anna",
            "Olya",
            "Max",
            "Kasia",
            "Piotr",
            "Zoe");
        var members = rows.Select(member => member.Id).ToArray();
        var random = new Random(20_261_023);
        var currencies = new (string Code, decimal? Rate)[] { ("EUR", null), ("PLN", 0.23m), ("JPY", 0.0061m), ("KWD", 3.0m) };

        for (var index = 0; index < 30; index++)
        {
            var (code, rate) = currencies[random.Next(currencies.Length)];
            var amount = random.Next(500, 400_000);
            await PostEntryAsync(
                client,
                $"{GroupsPath}/{group.Id}/expenses",
                ExpenseBody(random, members, code, rate, amount, random.Next(3)));
        }

        for (var index = 0; index < 10; index++)
        {
            var (code, rate) = currencies[random.Next(currencies.Length)];
            var from = random.Next(members.Length);
            var to = (from + random.Next(1, members.Length)) % members.Length;
            var repayment = new Dictionary<string, object?>
            {
                ["fromMemberId"] = members[from],
                ["toMemberId"] = members[to],
                ["amount"] = random.Next(100, 50_000),
                ["currency"] = code,
                ["date"] = IsoDay(Today)
            };
            if (rate is not null)
            {
                repayment["rate"] = rate;
            }

            await PostEntryAsync(client, $"{GroupsPath}/{group.Id}/repayments", repayment);
        }

        var body = await GetBalancesAsync(client, group.Id);

        var figures = MemberFigures(body);
        figures.Select(member => member.Id).ShouldBe(members);
        figures.Sum(member => member.Balance).ShouldBe(0);
        figures.Sum(member => member.Sent).ShouldBe(figures.Sum(member => member.Received));
        foreach (var member in figures)
        {
            member.Balance.ShouldBe(member.Paid - member.Share + member.Sent - member.Received);
        }

        var expenses = (await GetAsync(client, $"{GroupsPath}/{group.Id}/expenses?pageSize=100"))
            .GetProperty("items").EnumerateArray().ToList();
        expenses.Count.ShouldBe(30);
        body.GetProperty("total").GetInt64().ShouldBe(expenses.Sum(expense => expense.GetProperty("groupAmount").GetInt64()));
        figures.Sum(member => member.Paid).ShouldBe(body.GetProperty("total").GetInt64());
        figures.Sum(member => member.Share).ShouldBe(body.GetProperty("total").GetInt64());
        foreach (var member in figures)
        {
            member.Paid.ShouldBe(expenses
                .Where(expense => expense.GetProperty("payerId").GetGuid() == member.Id)
                .Sum(expense => expense.GetProperty("groupAmount").GetInt64()));
            member.Share.ShouldBe(expenses
                .SelectMany(expense => expense.GetProperty("shares").EnumerateArray())
                .Where(share => share.GetProperty("memberId").GetGuid() == member.Id)
                .Sum(share => share.GetProperty("groupAmount").GetInt64()));
        }

        var remaining = figures.ToDictionary(member => member.Id, member => member.Balance);
        var transfers = TransferFigures(body);
        transfers.ShouldAllBe(transfer => transfer.Amount > 0);
        foreach (var (from, to, amount) in transfers)
        {
            remaining[from] += amount;
            remaining[to] -= amount;
        }

        remaining.Values.ShouldAllBe(balance => balance == 0);
        transfers.Count.ShouldBeLessThan(members.Length);
    }

    [Fact]
    public async Task Should_Return404_When_GroupUnknownOrForeign()
    {
        using var owner = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        var (group, _) = await TestData.AddGroupAsync(factory, await UserIdAsync(owner));
        using var stranger = new AuthClient(factory);
        await stranger.RegisterNewAsync(Ct);

        using var foreign = await stranger.Http.GetAsync($"{GroupsPath}/{group.Id}/balances", Ct);
        using var foreignView = await stranger.Http.GetAsync($"{GroupsPath}/{group.Id}/balances?currency=PLN", Ct);
        using var unknown = await owner.Http.GetAsync($"{GroupsPath}/{Guid.NewGuid()}/balances", Ct);
        using var notAGuid = await owner.Http.GetAsync($"{GroupsPath}/not-a-guid/balances", Ct);
        using var invalid = await owner.Http.GetAsync($"{GroupsPath}/{Guid.NewGuid()}/balances?currency=eur", Ct);
        using var anonymous = factory.CreateClient();
        using var unauthorized = await anonymous.GetAsync($"{GroupsPath}/{group.Id}/balances", Ct);

        foreign.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        foreignView.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        notAGuid.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        unauthorized.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await GetBalancesAsync(owner, group.Id)).GetProperty("total").GetInt64().ShouldBe(0);
    }

    private static Dictionary<string, object?> ExpenseBody(
        Random random,
        Guid[] members,
        string currency,
        decimal? rate,
        int amount,
        int kind)
    {
        var body = new Dictionary<string, object?>
        {
            ["payerId"] = members[random.Next(members.Length)],
            ["amount"] = amount,
            ["currency"] = currency,
            ["date"] = IsoDay(Today)
        };
        if (rate is not null)
        {
            body["rate"] = rate;
        }

        if (kind == 0)
        {
            return body;
        }

        var picked = members.OrderBy(_ => random.Next()).Take(random.Next(1, members.Length + 1)).ToArray();
        if (kind == 1)
        {
            body["split"] = new { mode = "equal", memberIds = picked };
            return body;
        }

        var weights = picked.Select(_ => random.Next(1, 100)).ToArray();
        var extra = amount - picked.Length;
        var shares = new long[picked.Length];
        Array.Fill(shares, 1L);
        long assigned = 0;
        for (var index = 0; index < picked.Length - 1; index++)
        {
            var part = extra * weights[index] / weights.Sum();
            shares[index] += part;
            assigned += part;
        }

        shares[^1] += extra - assigned;
        body["split"] = new
        {
            mode = "exact",
            shares = picked.Select((memberId, index) => new { memberId, amount = shares[index] }).ToArray()
        };
        return body;
    }

    private static async Task SeedRatesAsync(WebApplicationFactory<Program> app)
    {
        var date = IsoDay(Today.AddDays(-9));
        await TestData.AddRatesAsync(
            app,
            (Currency.ALL, date, 0.0440m),
            (Currency.EUR, date, 4.30m),
            (Currency.EUR, IsoDay(Today), 4.30m));
    }

    private static async Task<(Guid GroupId, Guid[] Members)> SeedAlbaniaAsync(
        WebApplicationFactory<Program> app,
        AuthClient client)
    {
        var (group, rows) = await TestData.AddGroupAsync(
            app,
            await UserIdAsync(client),
            "Albania 2026",
            Currency.EUR,
            "Anna",
            "Olya",
            "Max",
            "Kasia");
        var (anna, olya, max, kasia) = (rows[0].Id, rows[1].Id, rows[2].Id, rows[3].Id);
        var date = IsoDay(Today.AddDays(-9));
        var expenses = $"{GroupsPath}/{group.Id}/expenses";

        await PostEntryAsync(client, expenses, new { payerId = anna, amount = 12_000, currency = "EUR", date });
        await PostEntryAsync(client, expenses, new { payerId = olya, amount = 300_000, currency = "ALL", date });
        await PostEntryAsync(
            client,
            expenses,
            new
            {
                payerId = max,
                amount = 4_000,
                currency = "EUR",
                date,
                split = new { mode = "equal", memberIds = new[] { anna, max } }
            });
        await PostEntryAsync(
            client,
            $"{GroupsPath}/{group.Id}/repayments",
            new { fromMemberId = kasia, toMemberId = anna, amount = 2_000, currency = "EUR", date });
        var removed = await PostEntryAsync(client, expenses, new { payerId = kasia, amount = 5_000, currency = "EUR", date });
        using var deleted = await Send(client, HttpMethod.Delete, $"{expenses}/{removed.GetProperty("id").GetGuid()}");
        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var repayments = $"{GroupsPath}/{group.Id}/repayments";
        var mistaken = await PostEntryAsync(
            client,
            repayments,
            new { fromMemberId = olya, toMemberId = max, amount = 1_500, currency = "EUR", date });
        using var undone = await Send(client, HttpMethod.Delete, $"{repayments}/{mistaken.GetProperty("id").GetGuid()}");
        undone.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        return (group.Id, rows.Select(member => member.Id).ToArray());
    }

    private static List<(Guid Id, long Paid, long Share, long Sent, long Received, long Balance)> MemberFigures(
        JsonElement body) =>
        body.GetProperty("members").EnumerateArray()
            .Select(member => (
                member.GetProperty("memberId").GetGuid(),
                member.GetProperty("paid").GetInt64(),
                member.GetProperty("share").GetInt64(),
                member.GetProperty("sent").GetInt64(),
                member.GetProperty("received").GetInt64(),
                member.GetProperty("balance").GetInt64()))
            .ToList();

    private static List<(Guid From, Guid To, long Amount)> TransferFigures(JsonElement body) =>
        body.GetProperty("transfers").EnumerateArray()
            .Select(transfer => (
                transfer.GetProperty("fromMemberId").GetGuid(),
                transfer.GetProperty("toMemberId").GetGuid(),
                transfer.GetProperty("amount").GetInt64()))
            .ToList();

    private static async Task<JsonElement> GetBalancesAsync(AuthClient client, Guid groupId, string? currency = null) =>
        await GetAsync(client, $"{GroupsPath}/{groupId}/balances{(currency is null ? string.Empty : $"?currency={currency}")}");

    private static async Task<JsonElement> PostEntryAsync(AuthClient client, string path, object body)
    {
        using var response = await Send(client, HttpMethod.Post, path, body);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static Task<HttpResponseMessage> Send(AuthClient client, HttpMethod method, string path, object? body = null)
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
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task<Guid> UserIdAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync("/v1/me", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private static string IsoDay(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
