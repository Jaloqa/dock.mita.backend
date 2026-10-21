using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.Common.Tags;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Tags;

public sealed class TagWriterTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_InsertMissingAndReuseExisting_When_Upserting()
    {
        var userId = await NewUserAsync();
        var trip = await TestData.AddTagAsync(factory, userId, "Trip");

        var ids = await UpsertAsync(userId, ["TRIP", "Food"]);

        ids.Count.ShouldBe(2);
        ids.ShouldContain(trip.Id);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Tags.Where(tag => tag.UserId == userId).Select(tag => tag.Name).ToListAsync(Ct))
            .ShouldBe(["Trip", "Food"], ignoreOrder: true);
    }

    [Fact]
    public async Task Should_ReturnEmpty_When_NoNames()
    {
        var userId = await NewUserAsync();

        (await UpsertAsync(userId, [])).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_CreateOneTag_When_UpsertedConcurrently()
    {
        var userId = await NewUserAsync();
        using var start = new Barrier(10);

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Factory.StartNew(
            () =>
            {
                start.SignalAndWait(Ct);
                return UpsertAsync(userId, ["Shared"]);
            },
            Ct,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap()));

        results.Select(ids => ids.Single()).Distinct().Count().ShouldBe(1);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Tags.CountAsync(tag => tag.UserId == userId, Ct)).ShouldBe(1);
    }

    private async Task<IReadOnlyList<Guid>> UpsertAsync(Guid userId, IReadOnlyList<string> names)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.UpsertAsync(userId, names, Ct);
    }

    private async Task<Guid> NewUserAsync()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        using var response = await client.Http.GetAsync("/v1/me", Ct);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }
}
