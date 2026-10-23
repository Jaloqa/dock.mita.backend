using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Groups;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Groups;

public sealed class GroupShareTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string GroupsPath = "/v1/groups";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_Return401_When_NotAuthenticated()
    {
        using var client = new AuthClient(factory);
        var path = $"{GroupsPath}/{Guid.NewGuid()}/share";

        using var turnOn = await client.Http.PostAsync(path, null, Ct);
        using var turnOff = await client.Http.DeleteAsync(path, Ct);

        turnOn.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        turnOff.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_TurnOnLink()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, _) = await TestData.AddGroupAsync(factory, await UserIdAsync(client), "Trip");
        (await ReadGroupAsync(client, group.Id)).GetProperty("shareToken").ValueKind.ShouldBe(JsonValueKind.Null);

        using var response = await SendAsync(client, HttpMethod.Post, group.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("id").GetGuid().ShouldBe(group.Id);
        body.GetProperty("name").GetString().ShouldBe("Trip");
        body.GetProperty("members").GetArrayLength().ShouldBe(3);
        var token = body.GetProperty("shareToken").GetString();
        ShareTokens.IsWellFormed(token).ShouldBeTrue();
        (await ReadGroupAsync(client, group.Id)).GetProperty("shareToken").GetString().ShouldBe(token);
        (await StoredTokenAsync(group.Id)).ShouldBe(token);
    }

    [Fact]
    public async Task Should_ReplaceToken_When_LinkRegenerated()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, _) = await TestData.AddGroupAsync(factory, await UserIdAsync(client), "Trip");
        var first = await ShareAsync(client, group.Id);

        using var response = await SendAsync(client, HttpMethod.Post, group.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var second = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("shareToken").GetString()!;
        ShareTokens.IsWellFormed(second).ShouldBeTrue();
        second.ShouldNotBe(first);
        (await StoredTokenAsync(group.Id)).ShouldBe(second);
        (await GroupCountWithTokenAsync(first)).ShouldBe(0);
        (await GroupCountWithTokenAsync(second)).ShouldBe(1);
        (await ReadGroupAsync(client, group.Id)).GetProperty("shareToken").GetString().ShouldBe(second);
    }

    [Fact]
    public async Task Should_TurnOffLink()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, _) = await TestData.AddGroupAsync(factory, await UserIdAsync(client), "Trip");
        var token = await ShareAsync(client, group.Id);

        using var response = await SendAsync(client, HttpMethod.Delete, group.Id);
        using var again = await SendAsync(client, HttpMethod.Delete, group.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        again.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ReadGroupAsync(client, group.Id)).GetProperty("shareToken").ValueKind.ShouldBe(JsonValueKind.Null);
        (await StoredTokenAsync(group.Id)).ShouldBeNull();
        (await GroupCountWithTokenAsync(token)).ShouldBe(0);
    }

    [Fact]
    public async Task Should_TurnOffLink_When_LinkWasNeverOn()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, _) = await TestData.AddGroupAsync(factory, await UserIdAsync(client), "Trip");

        using var response = await SendAsync(client, HttpMethod.Delete, group.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await StoredTokenAsync(group.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task Should_TurnOnLinkAgain_When_LinkWasTurnedOff()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, _) = await TestData.AddGroupAsync(factory, await UserIdAsync(client), "Trip");
        var first = await ShareAsync(client, group.Id);
        using var off = await SendAsync(client, HttpMethod.Delete, group.Id);

        var second = await ShareAsync(client, group.Id);

        second.ShouldNotBe(first);
        (await StoredTokenAsync(group.Id)).ShouldBe(second);
    }

    [Fact]
    public async Task Should_KeepGroupsApart_When_OneLinkChanges()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var (shared, _) = await TestData.AddGroupAsync(factory, userId, "Shared");
        var (neighbour, _) = await TestData.AddGroupAsync(factory, userId, "Neighbour");
        var neighbourToken = await ShareAsync(client, neighbour.Id);

        await ShareAsync(client, shared.Id);
        using var off = await SendAsync(client, HttpMethod.Delete, shared.Id);

        (await StoredTokenAsync(shared.Id)).ShouldBeNull();
        (await StoredTokenAsync(neighbour.Id)).ShouldBe(neighbourToken);
    }

    [Fact]
    public async Task Should_Return404_When_GroupUnknownOrForeign()
    {
        using var owner = new AuthClient(factory);
        using var intruder = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        await intruder.RegisterNewAsync(Ct);
        var (shared, _) = await TestData.AddGroupAsync(factory, await UserIdAsync(owner), "Shared");
        var (closed, _) = await TestData.AddGroupAsync(factory, await UserIdAsync(owner), "Closed");
        var token = await ShareAsync(owner, shared.Id);
        var unknown = Guid.NewGuid();

        using var unknownPost = await SendAsync(owner, HttpMethod.Post, unknown);
        using var unknownDelete = await SendAsync(owner, HttpMethod.Delete, unknown);
        using var foreignPost = await SendAsync(intruder, HttpMethod.Post, shared.Id);
        using var foreignDelete = await SendAsync(intruder, HttpMethod.Delete, shared.Id);
        using var foreignClosedPost = await SendAsync(intruder, HttpMethod.Post, closed.Id);
        using var notAGuid = await owner.Http.SendAsync(
            owner.WithXsrf(new HttpRequestMessage(HttpMethod.Post, $"{GroupsPath}/not-a-guid/share")),
            Ct);

        unknownPost.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        unknownDelete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        foreignPost.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        foreignDelete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        foreignClosedPost.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        notAGuid.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await StoredTokenAsync(shared.Id)).ShouldBe(token);
        (await StoredTokenAsync(closed.Id)).ShouldBeNull();
        (await ReadGroupAsync(owner, shared.Id)).GetProperty("shareToken").GetString().ShouldBe(token);
    }

    [Fact]
    public async Task Should_Return403_When_XsrfTokenMissing()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var (group, _) = await TestData.AddGroupAsync(factory, await UserIdAsync(client), "Trip");
        var path = $"{GroupsPath}/{group.Id}/share";

        using var turnOn = await client.Http.PostAsync(path, null, Ct);
        using var turnOff = await client.Http.DeleteAsync(path, Ct);

        turnOn.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        turnOff.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await StoredTokenAsync(group.Id)).ShouldBeNull();
    }

    private static Task<HttpResponseMessage> SendAsync(AuthClient client, HttpMethod method, Guid id) =>
        client.Http.SendAsync(client.WithXsrf(new HttpRequestMessage(method, $"{GroupsPath}/{id}/share")), Ct);

    private static async Task<string> ShareAsync(AuthClient client, Guid id)
    {
        using var response = await SendAsync(client, HttpMethod.Post, id);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("shareToken").GetString()!;
    }

    private static async Task<JsonElement> ReadGroupAsync(AuthClient client, Guid id)
    {
        using var response = await client.Http.GetAsync($"{GroupsPath}/{id}", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private async Task<string?> StoredTokenAsync(Guid groupId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Groups.AsNoTracking()
            .Where(row => row.Id == groupId)
            .Select(row => row.ShareToken)
            .SingleAsync(Ct);
    }

    private async Task<int> GroupCountWithTokenAsync(string token)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Groups.CountAsync(row => row.ShareToken == token, Ct);
    }

    private static async Task<Guid> UserIdAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync("/v1/me", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }
}
