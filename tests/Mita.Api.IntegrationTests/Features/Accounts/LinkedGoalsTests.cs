using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Accounts;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Accounts;

public sealed class LinkedGoalsTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_CountLinkedGoals()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var savings = await TestData.AddAccountAsync(factory, userId, type: AccountType.Savings);
        var spare = await TestData.AddAccountAsync(factory, userId, type: AccountType.Savings);
        var card = await TestData.AddAccountAsync(factory, userId);
        var before = await LinkedGoalsAsync(client);

        using var first = await SendAsync(client, HttpMethod.Post, "/v1/goals", new { name = "Car", target = 1_000, accountId = savings.Id });
        using var second = await SendAsync(client, HttpMethod.Post, "/v1/goals", new { name = "Flat", target = 1_000, accountId = savings.Id });

        before[savings.Id].ShouldBe(0);
        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.Created);
        var after = await LinkedGoalsAsync(client);
        after[savings.Id].ShouldBe(2);
        after[spare.Id].ShouldBe(0);
        after[card.Id].ShouldBe(0);
    }

    [Fact]
    public async Task Should_UnlinkGoals_When_AccountDeleted()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var savings = await TestData.AddAccountAsync(factory, userId, type: AccountType.Savings);
        await TestData.AddGoalAsync(factory, userId, "Car", 1_000, accountId: savings.Id);
        await TestData.AddGoalAsync(factory, userId, "Flat", 1_000, accountId: savings.Id);

        using var delete = await SendAsync(client, HttpMethod.Delete, $"/v1/accounts/{savings.Id}", null);

        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var list = await client.Http.GetAsync("/v1/goals", Ct);
        var items = (await list.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items");
        items.GetArrayLength().ShouldBe(2);
        items.EnumerateArray().ShouldAllBe(item => item.GetProperty("accountId").ValueKind == JsonValueKind.Null);
    }

    private static async Task<Dictionary<Guid, int>> LinkedGoalsAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync("/v1/accounts?pageSize=100", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return page.GetProperty("items").EnumerateArray().ToDictionary(
            item => item.GetProperty("id").GetGuid(),
            item => item.GetProperty("linkedGoals").GetInt32());
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

    private static async Task<Guid> UserIdAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync("/v1/me", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }
}
