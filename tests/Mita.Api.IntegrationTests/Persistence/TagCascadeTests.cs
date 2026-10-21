using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Tags;
using Mita.Infrastructure.Persistence;
using Npgsql;
using Shouldly;

namespace Mita.Api.IntegrationTests.Persistence;

public sealed class TagCascadeTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_RejectSecondTag_When_NameDiffersOnlyInCase()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        using var other = new AuthClient(factory);
        await other.RegisterNewAsync(Ct);
        var otherUserId = await UserIdAsync(other);
        await TestData.AddTagAsync(factory, userId, "Trip");

        var failure = await Should.ThrowAsync<DbUpdateException>(
            () => TestData.AddTagAsync(factory, userId, "TRIP"));

        var postgres = failure.InnerException.ShouldBeOfType<PostgresException>();
        postgres.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        postgres.ConstraintName.ShouldBe("IX_tags_user_name");
        var created = await TestData.AddTagAsync(factory, otherUserId, "TRIP");
        created.Name.ShouldBe("TRIP");
    }

    [Fact]
    public async Task Should_RemoveJoinRows_When_TagOrTransactionDeleted()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(factory, userId, openingBalance: 10_000);
        var category = await TestData.AddCategoryAsync(factory, userId);
        var transaction = await TestData.AddTransactionAsync(factory, account, category, 1_200);
        var a = await TestData.AddTagAsync(factory, userId, "a");
        var b = await TestData.AddTagAsync(factory, userId, "b");
        await TestData.TagTransactionAsync(factory, transaction, a, b);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Tags.Where(tag => tag.Id == a.Id).ExecuteDeleteAsync(Ct);
        (await db.TransactionTags.Where(row => row.TransactionId == transaction.Id).Select(row => row.TagId).ToListAsync(Ct))
            .ShouldBe([b.Id]);

        await db.Transactions.Where(row => row.Id == transaction.Id).ExecuteDeleteAsync(Ct);

        (await db.TransactionTags.AnyAsync(row => row.TransactionId == transaction.Id, Ct)).ShouldBeFalse();
        (await db.Tags.AnyAsync(tag => tag.Id == b.Id, Ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_DeleteTags_When_UserDeleted()
    {
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(factory, userId, openingBalance: 10_000);
        var category = await TestData.AddCategoryAsync(factory, userId);
        var transaction = await TestData.AddTransactionAsync(factory, account, category, 1_200);
        var tag = await TestData.AddTagAsync(factory, userId, "a");
        await TestData.TagTransactionAsync(factory, transaction, tag);

        using var request = client.WithXsrf(
            new HttpRequestMessage(HttpMethod.Delete, "/v1/me")
            {
                Content = JsonContent.Create(new { password = credentials.Password })
            });
        using var response = await client.Http.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Tags.AnyAsync(row => row.UserId == userId, Ct)).ShouldBeFalse();
        (await db.TransactionTags.AnyAsync(row => row.TransactionId == transaction.Id, Ct)).ShouldBeFalse();
    }

    private static async Task<Guid> UserIdAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync("/v1/me", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }
}
