using Mita.Api.Common;
using Mita.Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Mita.Api.IntegrationTests;

public sealed class ToPageAsyncTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Should_ReturnTotalAndRequestedSlice_When_PagingAQuery()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new SettableTimeProvider(DateTimeOffset.UnixEpoch);
        await using var context = await AuditedThingDbContext.CreateAsync(postgres.NewDatabaseConnectionString(), clock, ct);
        context.Things.AddRange(Enumerable.Range(1, 25).Select(i => new AuditedThing { Name = $"thing-{i:00}" }));
        await context.SaveChangesAsync(ct);

        var page = await context.Things.OrderBy(t => t.Name).Select(t => t.Name).ToPageAsync(new PageRequest(3, 10), ct);

        page.Total.ShouldBe(25);
        page.Page.ShouldBe(3);
        page.PageSize.ShouldBe(10);
        page.Items.ShouldBe(["thing-21", "thing-22", "thing-23", "thing-24", "thing-25"]);
    }
}
