using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.Authentication;
using Mita.Api.Common.Groups;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Groups;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;
using Npgsql;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Groups;

public sealed class GroupAccessTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(GroupLock.None)]
    [InlineData(GroupLock.Share)]
    [InlineData(GroupLock.Update)]
    public async Task Should_OpenOwnGroup_When_AnyLockRequested(GroupLock mode)
    {
        var (ownerId, group) = await NewGroupAsync();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(Ct);

        var context = await new GroupAccess(db, new FixedUser(ownerId)).OpenOwnedAsync(group.Id, mode, Ct);

        context.ShouldBe(new GroupContext(group.Id, ownerId, Currency.EUR, GroupActor.Owner(ownerId)));
        var scoped = await new GroupAccess(db, new FixedUser(ownerId)).OpenAsync(new GroupScope(group.Id), mode, Ct);
        scoped.ShouldBe(context);
    }

    [Theory]
    [InlineData(GroupLock.None)]
    [InlineData(GroupLock.Share)]
    [InlineData(GroupLock.Update)]
    public async Task Should_ReturnNull_When_GroupUnknownOrForeign(GroupLock mode)
    {
        var (_, group) = await NewGroupAsync();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(Ct);

        var foreign = await new GroupAccess(db, new FixedUser(Guid.NewGuid())).OpenOwnedAsync(group.Id, mode, Ct);
        var unknown = await new GroupAccess(db, new FixedUser(group.OwnerId)).OpenOwnedAsync(Guid.NewGuid(), mode, Ct);

        foreign.ShouldBeNull();
        unknown.ShouldBeNull();
    }

    [Theory]
    [InlineData(GroupLock.Share, GroupLock.Share, false)]
    [InlineData(GroupLock.Share, GroupLock.Update, true)]
    [InlineData(GroupLock.Update, GroupLock.Share, true)]
    [InlineData(GroupLock.Update, GroupLock.Update, true)]
    [InlineData(GroupLock.Update, GroupLock.None, false)]
    [InlineData(GroupLock.None, GroupLock.Update, false)]
    public async Task Should_LockTheGroupRow_When_LockRequested(GroupLock held, GroupLock requested, bool blocks)
    {
        var (ownerId, group) = await NewGroupAsync();

        await using var holderScope = factory.Services.CreateAsyncScope();
        var holderDb = holderScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var holder = await holderDb.Database.BeginTransactionAsync(Ct);
        (await new GroupAccess(holderDb, new FixedUser(ownerId)).OpenOwnedAsync(group.Id, held, Ct)).ShouldNotBeNull();

        await using var waiterScope = factory.Services.CreateAsyncScope();
        var waiterDb = waiterScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var waiter = await waiterDb.Database.BeginTransactionAsync(Ct);
        await waiterDb.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '300ms'", Ct);

        var blocked = false;
        try
        {
            (await new GroupAccess(waiterDb, new FixedUser(ownerId)).OpenOwnedAsync(group.Id, requested, Ct))
                .ShouldNotBeNull();
        }
        catch (InvalidOperationException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.LockNotAvailable })
        {
            blocked = true;
        }

        blocked.ShouldBe(blocks);
    }

    private async Task<(Guid OwnerId, Group Group)> NewGroupAsync()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        using var response = await client.Http.GetAsync("/v1/me", Ct);
        var ownerId = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
        var (group, _) = await TestData.AddGroupAsync(factory, ownerId);
        return (ownerId, group);
    }

    private sealed class FixedUser(Guid id) : ICurrentUser
    {
        public Guid Id => id;
    }
}
