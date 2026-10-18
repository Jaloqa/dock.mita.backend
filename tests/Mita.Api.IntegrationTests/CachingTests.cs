using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Mita.Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Mita.Api.IntegrationTests;

public sealed class CachingTests(MitaApiFactory factory, RedisFixture redis) : IClassFixture<MitaApiFactory>
{
    [Fact]
    public async Task Should_ServeFromCache_When_CounterRequestedTwice()
    {
        using var client = await CreateClientAsync(factory);

        var first = await GetCounterAsync(client, "twice");
        var second = await GetCounterAsync(client, "twice");

        first.ShouldBe(1);
        second.ShouldBe(1);
    }

    [Fact]
    public async Task Should_RecomputeAfterReset()
    {
        using var client = await CreateClientAsync(factory);
        var before = await GetCounterAsync(client, "reset");

        await ResetAsync(client);
        var after = await GetCounterAsync(client, "reset");

        before.ShouldBe(1);
        after.ShouldBe(2);
    }

    [Fact]
    public async Task Should_IsolateUsers()
    {
        using var firstUser = await CreateClientAsync(factory);
        using var secondUser = await CreateClientAsync(factory);
        await GetCounterAsync(firstUser, "shared");
        await ResetAsync(firstUser);

        var firstValue = await GetCounterAsync(firstUser, "shared");
        var secondValue = await GetCounterAsync(secondUser, "shared");
        var firstAgain = await GetCounterAsync(firstUser, "shared");
        var secondAgain = await GetCounterAsync(secondUser, "shared");

        firstValue.ShouldBe(2);
        secondValue.ShouldBe(1);
        firstAgain.ShouldBe(2);
        secondAgain.ShouldBe(1);
    }

    [Fact]
    public async Task Should_StoreEntryInRedis()
    {
        var user = await TestTokens.CreateForNewUserAsync(factory);
        var userId = user.UserId;
        using var client = CreateClient(factory, user);

        await GetCounterAsync(client, "stored");

        var server = redis.Multiplexer.GetServer(redis.Multiplexer.GetEndPoints()[0]);
        var keys = new List<string>();
        await foreach (var key in server.KeysAsync(pattern: $"mita:u:{userId}:*")
            .WithCancellation(TestContext.Current.CancellationToken))
        {
            keys.Add(key.ToString());
        }

        keys.ShouldBe([$"mita:u:{userId}:counter:stored"]);
    }

    [Fact]
    public async Task Should_WorkAndReportDegraded_When_RedisDown()
    {
        await using var redisDown = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(
            (_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Redis"] = "127.0.0.1:1"
            })));
        using var client = await CreateClientAsync(redisDown);

        var first = await GetCounterAsync(client, "down");
        var second = await GetCounterAsync(client, "down");
        using var ready = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        first.ShouldBe(1);
        second.ShouldBe(1);
        ready.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ready.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("Degraded");
    }

    private static async Task<HttpClient> CreateClientAsync(WebApplicationFactory<Program> app) =>
        CreateClient(app, await TestTokens.CreateForNewUserAsync(app));

    private static HttpClient CreateClient(WebApplicationFactory<Program> app, TestUserToken user)
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        return client;
    }

    private static async Task<int> GetCounterAsync(HttpClient client, string name)
    {
        using var response = await client.GetAsync(
            $"/v1/test/counter?name={name}",
            TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("value").GetInt32();
    }

    private static async Task ResetAsync(HttpClient client)
    {
        using var response = await client.PostAsync(
            "/v1/test/counter/reset",
            null,
            TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
