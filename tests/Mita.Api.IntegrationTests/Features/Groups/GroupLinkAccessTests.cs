using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Elastic.Apm.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Groups;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;
using Moq;
using Serilog.Core;
using Serilog.Events;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Groups;

public sealed class GroupLinkAccessTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string GroupsPath = "/v1/groups";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Should_ReadGroupAnonymously_ThroughLink()
    {
        await using var isolated = factory.WithIsolatedData();
        using var owner = new AuthClient(isolated);
        await owner.RegisterNewAsync(Ct);
        var shared = await SeedSharedGroupAsync(isolated, owner, "Albania 2026");
        await TestData.AddRatesAsync(
            isolated,
            (Currency.EUR, IsoDay(Today), 4.30m),
            (Currency.USD, IsoDay(Today), 3.95m));
        using var link = new LinkClient(isolated);

        using var groupResponse = await link.GetAsync(shared.Token);

        groupResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var linkGroup = await ReadNodeAsync(groupResponse);
        linkGroup["shareToken"].ShouldBeNull();
        linkGroup["members"]!.AsArray().Count.ShouldBe(3);
        var ownerGroup = await OwnerNodeAsync(owner, GroupPath(shared.GroupId));
        ownerGroup["shareToken"]!.GetValue<string>().ShouldBe(shared.Token);
        ownerGroup["shareToken"] = null;
        linkGroup.ToJsonString().ShouldBe(ownerGroup.ToJsonString());

        var reads = new (string Link, string Owner, int Items)[]
        {
            ("/expenses", $"{GroupPath(shared.GroupId)}/expenses", 2),
            ("/expenses?deleted=true", $"{GroupPath(shared.GroupId)}/expenses?deleted=true", 1),
            ("/repayments", $"{GroupPath(shared.GroupId)}/repayments", 1),
            (
                $"/expenses/{shared.ExpenseId}/history",
                $"{GroupPath(shared.GroupId)}/expenses/{shared.ExpenseId}/history",
                1
            ),
            (
                $"/repayments/{shared.RepaymentId}/history",
                $"{GroupPath(shared.GroupId)}/repayments/{shared.RepaymentId}/history",
                1
            )
        };
        foreach (var (linkPath, ownerPath, items) in reads)
        {
            using var response = await link.GetAsync(shared.Token, linkPath);

            response.StatusCode.ShouldBe(HttpStatusCode.OK, linkPath);
            var body = await ReadNodeAsync(response);
            body["items"]!.AsArray().Count.ShouldBe(items, linkPath);
            body.ToJsonString().ShouldBe((await OwnerNodeAsync(owner, ownerPath)).ToJsonString(), linkPath);
        }

        using var balances = await link.GetAsync(shared.Token, "/balances");
        balances.StatusCode.ShouldBe(HttpStatusCode.OK);
        var linkBalances = await ReadNodeAsync(balances);
        linkBalances["total"]!.GetValue<long>().ShouldBe(12_000);
        linkBalances.ToJsonString()
            .ShouldBe((await OwnerNodeAsync(owner, $"{GroupPath(shared.GroupId)}/balances")).ToJsonString());

        using var rates = await link.GetAsync(shared.Token, "/rates?to=EUR");
        rates.StatusCode.ShouldBe(HttpStatusCode.OK);
        var linkRates = await ReadNodeAsync(rates);
        linkRates.AsArray().Count.ShouldBe(2);
        linkRates.ToJsonString().ShouldBe((await OwnerNodeAsync(owner, "/v1/rates?to=EUR")).ToJsonString());
    }

    [Fact]
    public async Task Should_Return404_When_TokenUnknownMalformedOrOff()
    {
        using var owner = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        var shared = await SeedSharedGroupAsync(factory, owner, "Trip");
        using var link = new LinkClient(factory);
        using (var working = await link.GetAsync(shared.Token))
        {
            working.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        string[] bad = [ShareTokens.New(), "abc", $"{shared.Token}A"];
        foreach (var token in bad)
        {
            await ShouldBeNotFoundProblemAsync(link, token, string.Empty);
            await ShouldBeNotFoundProblemAsync(link, token, "/expenses");
        }

        using (var off = await owner.Http.SendAsync(
            owner.WithXsrf(new HttpRequestMessage(HttpMethod.Delete, $"{GroupPath(shared.GroupId)}/share")),
            Ct))
        {
            off.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        await ShouldBeNotFoundProblemAsync(link, shared.Token, string.Empty);
        await ShouldBeNotFoundProblemAsync(link, shared.Token, "/expenses");
    }

    [Fact]
    public async Task Should_ConfineLink_ToItsGroup()
    {
        using var owner = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        var shared = await SeedSharedGroupAsync(factory, owner, "Shared");
        var other = await SeedSharedGroupAsync(factory, owner, "Other");
        using var link = new LinkClient(factory);

        using var foreignExpense = await link.GetAsync(shared.Token, $"/expenses/{other.ExpenseId}/history");
        using var foreignRepayment = await link.GetAsync(shared.Token, $"/repayments/{other.RepaymentId}/history");
        using var put = await link.SendAsync(HttpMethod.Put, shared.Token, string.Empty, new { name = "Renamed", currency = "PLN" });
        using var delete = await link.SendAsync(HttpMethod.Delete, shared.Token, string.Empty);
        using var addMember = await link.SendAsync(HttpMethod.Post, shared.Token, "/members", new { name = "Eve" });
        using var renameMember = await link.SendAsync(
            HttpMethod.Put,
            shared.Token,
            $"/members/{shared.MemberIds[0]}",
            new { name = "Eve" });
        using var shareOn = await link.SendAsync(HttpMethod.Post, shared.Token, "/share");
        using var shareOff = await link.SendAsync(HttpMethod.Delete, shared.Token, "/share");
        using var ownerGroup = await link.Http.GetAsync(GroupPath(shared.GroupId), Ct);
        using var accounts = await link.Http.GetAsync("/v1/accounts", Ct);
        using var me = await link.Http.GetAsync("/v1/me", Ct);

        foreignExpense.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        foreignRepayment.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        put.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        delete.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        addMember.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        renameMember.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        shareOn.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        shareOff.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ownerGroup.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        accounts.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        me.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var stored = await StoredGroupAsync(shared.GroupId);
        stored.Name.ShouldBe("Shared");
        stored.ShareToken.ShouldBe(shared.Token);
        stored.Members.ShouldBe(["Anna", "Olya", "Max"]);
    }

    [Fact]
    public async Task Should_IgnoreIdentity_OnLink()
    {
        using var owner = new AuthClient(factory);
        using var visitor = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        await visitor.RegisterNewAsync(Ct);
        var shared = await SeedSharedGroupAsync(factory, owner, "Trip");

        using var visitorLink = await visitor.Http.GetAsync(LinkClient.PathOf(shared.Token), Ct);
        using var visitorOwnerRoute = await visitor.Http.GetAsync(GroupPath(shared.GroupId), Ct);
        using var visitorUnknownLink = await visitor.Http.GetAsync(LinkClient.PathOf(ShareTokens.New()), Ct);
        using var ownerLink = await owner.Http.GetAsync(LinkClient.PathOf(shared.Token), Ct);

        visitorLink.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await visitorLink.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("id").GetGuid().ShouldBe(shared.GroupId);
        body.GetProperty("shareToken").ValueKind.ShouldBe(JsonValueKind.Null);
        visitorOwnerRoute.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        visitorUnknownLink.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await visitorUnknownLink.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("title").GetString()
            .ShouldBe("Not Found");
        ownerLink.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ownerLink.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("shareToken").ValueKind
            .ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Should_RateLimitLinkRoutes_IncludingUnknownTokens()
    {
        await using var limited = factory.WithSettings(
            new Dictionary<string, string?> { ["RateLimiting:GroupLinkPermitLimit"] = "2" });
        using var owner = new AuthClient(limited);
        await owner.RegisterNewAsync(Ct);
        var (group, _) = await TestData.AddGroupAsync(limited, await UserIdAsync(owner), "Trip");
        var token = await ShareAsync(owner, group.Id);
        using var link = new LinkClient(limited);
        var unknown = ShareTokens.New();

        using var first = await link.GetAsync(unknown);
        using var second = await link.GetAsync(unknown, "/expenses");
        using var third = await link.GetAsync(token);
        using var ownList = await owner.Http.GetAsync(GroupsPath, Ct);

        first.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        second.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        third.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        third.Headers.RetryAfter.ShouldNotBeNull();
        (await third.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("status").GetInt32().ShouldBe(429);
        ownList.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_MaskShareToken_When_LinkRequestsAreLogged()
    {
        var sink = new CapturingSink();
        await using var logged = factory
            .WithSettings(new Dictionary<string, string?>
            {
                ["Serilog:MinimumLevel:Default"] = "Verbose",
                ["Serilog:MinimumLevel:Override:Microsoft.AspNetCore"] = "Verbose",
                ["Serilog:MinimumLevel:Override:System"] = "Verbose"
            })
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                services => services.AddSingleton<ILogEventSink>(sink)));
        using var owner = new AuthClient(logged);
        await owner.RegisterNewAsync(Ct);
        var shared = await SeedSharedGroupAsync(logged, owner, "Trip");
        using var link = new LinkClient(logged);
        var missingExpense = Guid.NewGuid();

        using (var group = await link.GetAsync(shared.Token))
        {
            group.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using (var history = await link.GetAsync(shared.Token, $"/expenses/{missingExpense}/history"))
        {
            history.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        var events = sink.Events.ToArray();
        string.Join('\n', events.Select(Render)).ShouldNotContain(shared.Token);
        events
            .Where(logEvent => logEvent.Properties.ContainsKey("RequestMethod")
                && logEvent.Properties.ContainsKey("StatusCode"))
            .Select(logEvent => logEvent.Properties["RequestPath"].ToString())
            .Where(path => path.Contains("/shared/groups/", StringComparison.Ordinal))
            .ShouldBe(
                ["\"/v1/shared/groups/***\"", $"\"/v1/shared/groups/***/expenses/{missingExpense}/history\""],
                ignoreOrder: true);
        events
            .Where(logEvent => logEvent.Properties.ContainsKey("RequestName"))
            .Select(logEvent => logEvent.Properties.GetValueOrDefault("RequestPath")?.ToString())
            .ShouldContain("\"/v1/shared/groups/***\"");
    }

    [Fact]
    public async Task Should_NameApmTransactionsByRoute_When_LinkRequested()
    {
        var names = new ConcurrentQueue<string>();
        var transaction = new Mock<ITransaction>();
        transaction.SetupSet(target => target.Name = It.IsAny<string>()).Callback<string>(names.Enqueue);
        var tracer = Mock.Of<ITracer>(target => target.CurrentTransaction == transaction.Object);
        await using var traced = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton(tracer)));
        using var owner = new AuthClient(traced);
        await owner.RegisterNewAsync(Ct);
        var shared = await SeedSharedGroupAsync(traced, owner, "Trip");
        using var link = new LinkClient(traced);
        names.Clear();

        using var group = await link.GetAsync(shared.Token);
        using var missingHistory = await link.GetAsync(shared.Token, $"/expenses/{Guid.NewGuid()}/history");
        using var unknownToken = await link.GetAsync(ShareTokens.New(), "/expenses");
        using var put = await link.SendAsync(HttpMethod.Put, shared.Token, string.Empty);
        using var addMember = await link.SendAsync(HttpMethod.Post, shared.Token, "/members", new { name = "Eve" });
        using var ownerGroup = await owner.Http.GetAsync(GroupPath(shared.GroupId), Ct);

        group.StatusCode.ShouldBe(HttpStatusCode.OK);
        missingHistory.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        unknownToken.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        put.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        addMember.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ownerGroup.StatusCode.ShouldBe(HttpStatusCode.OK);
        names.ShouldBe(
        [
            "GET /v{version:apiVersion}/shared/groups/{token}/",
            "GET /v{version:apiVersion}/shared/groups/{token}/expenses/{expenseId:guid}/history",
            "GET /v{version:apiVersion}/shared/groups/{token}/expenses",
            "PUT unknown route",
            "POST unknown route",
            "GET /v{version:apiVersion}/groups/{id:guid}"
        ]);
    }

    private static async Task ShouldBeNotFoundProblemAsync(LinkClient link, string token, string path)
    {
        using var response = await link.GetAsync(token, path);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound, $"{token}{path}");
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("status").GetInt32().ShouldBe(404);
        problem.GetProperty("title").GetString().ShouldBe("Not Found");
    }

    private static async Task<SharedGroup> SeedSharedGroupAsync(
        WebApplicationFactory<Program> app,
        AuthClient owner,
        string name)
    {
        var (group, members) = await TestData.AddGroupAsync(app, await UserIdAsync(owner), name);
        var expense = await TestData.AddGroupExpenseAsync(app, group, members[0], 9_000, members);
        await TestData.AddGroupExpenseAsync(app, group, members[1], 3_000, [members[0], members[1]], "2026-10-02");
        await TestData.AddGroupExpenseAsync(app, group, members[2], 1_500, members, "2026-10-03", deleted: true);
        var repayment = await TestData.AddGroupRepaymentAsync(app, group, members[2], members[0], 2_000);
        var token = await ShareAsync(owner, group.Id);
        return new SharedGroup(group.Id, [.. members.Select(member => member.Id)], expense.Id, repayment.Id, token);
    }

    private static async Task<string> ShareAsync(AuthClient owner, Guid groupId)
    {
        using var response = await owner.Http.SendAsync(
            owner.WithXsrf(new HttpRequestMessage(HttpMethod.Post, $"{GroupPath(groupId)}/share")),
            Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("shareToken").GetString()!;
    }

    private static async Task<JsonNode> OwnerNodeAsync(AuthClient owner, string path)
    {
        using var response = await owner.Http.GetAsync(path, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, path);
        return await ReadNodeAsync(response);
    }

    private static async Task<JsonNode> ReadNodeAsync(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync(Ct))!;

    private async Task<(string Name, string? ShareToken, string[] Members)> StoredGroupAsync(Guid groupId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var group = await db.Groups.AsNoTracking().SingleAsync(row => row.Id == groupId, Ct);
        var members = await db.GroupMembers.AsNoTracking()
            .Where(member => member.GroupId == groupId)
            .OrderBy(member => member.Position)
            .Select(member => member.Name)
            .ToArrayAsync(Ct);
        return (group.Name, group.ShareToken, members);
    }

    private static async Task<Guid> UserIdAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync("/v1/me", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private static string GroupPath(Guid groupId) => $"{GroupsPath}/{groupId}";

    private static string IsoDay(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Render(LogEvent logEvent) =>
        string.Join(
            ' ',
            [
                logEvent.RenderMessage(CultureInfo.InvariantCulture), logEvent.MessageTemplate.Text,
                logEvent.Exception?.ToString() ?? string.Empty,
                .. logEvent.Properties.Select(property => $"{property.Key}={property.Value}")
            ]);

    private sealed record SharedGroup(Guid GroupId, Guid[] MemberIds, Guid ExpenseId, Guid RepaymentId, string Token);

    private sealed class CapturingSink : ILogEventSink
    {
        private readonly ConcurrentQueue<LogEvent> events = new();

        public IReadOnlyCollection<LogEvent> Events => events;

        public void Emit(LogEvent logEvent) => events.Enqueue(logEvent);
    }
}
