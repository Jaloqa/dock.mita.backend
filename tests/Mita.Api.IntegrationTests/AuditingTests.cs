using Mita.Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Mita.Api.IntegrationTests;

public sealed class AuditingTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Should_StampCreatedAndUpdated_When_Saving()
    {
        var ct = TestContext.Current.CancellationToken;
        var t1 = new DateTimeOffset(2026, 10, 18, 9, 0, 0, TimeSpan.Zero);
        var t2 = t1.AddHours(3);
        var clock = new SettableTimeProvider(t1);
        await using var context = await AuditedThingDbContext.CreateAsync(postgres.NewDatabaseConnectionString(), clock, ct);
        var thing = new AuditedThing { Name = "first" };

        context.Things.Add(thing);
        await context.SaveChangesAsync(ct);

        thing.CreatedAt.ShouldBe(t1);
        thing.UpdatedAt.ShouldBe(t1);

        clock.Now = t2;
        thing.Name = "second";
        await context.SaveChangesAsync(ct);

        thing.CreatedAt.ShouldBe(t1);
        thing.UpdatedAt.ShouldBe(t2);
    }
}
