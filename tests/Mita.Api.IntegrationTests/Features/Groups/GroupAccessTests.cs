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
        var scoped = await new GroupAccess(db, new FixedUser(ownerId)).OpenAsync(GroupScope.Owner(group.Id), mode, Ct);
        scoped.ShouldBe(context);
    }

    [Theory]
    [InlineData(GroupLock.None)]
    [InlineData(GroupLock.Share)]
    [InlineData(GroupLock.Update)]
    public async Task Should_OpenGroupByToken_When_LinkScopeAndAnyLockRequested(GroupLock mode)
    {
        var (ownerId, group) = await NewGroupAsync();
        var (_, neighbour) = await NewGroupAsync();
        var token = await ShareAsync(group.Id);
        await ShareAsync(neighbour.Id);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(Ct);
        var access = new GroupAccess(db, new AnonymousUser());

        var context = await access.OpenAsync(GroupScope.Link(token, null), mode, Ct);
        var unknown = await access.OpenAsync(GroupScope.Link(ShareTokens.New(), null), mode, Ct);

        context.ShouldNotBeNull();
        (context.GroupId, context.OwnerId, context.Currency).ShouldBe((group.Id, ownerId, Currency.EUR));
        unknown.ShouldBeNull();
    }

    [Fact]
    public async Task Should_ReturnNull_When_LinkTurnedOff()
    {
        var (_, group) = await NewGroupAsync();
        var token = await ShareAsync(group.Id);
        await using (var turnOff = factory.Services.CreateAsyncScope())
        {
            var writer = turnOff.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await writer.Groups.SingleAsync(row => row.Id == group.Id, Ct);
            stored.TurnOffShareLink();
            await writer.SaveChangesAsync(Ct);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        (await new GroupAccess(db, new AnonymousUser()).OpenAsync(GroupScope.Link(token, null), GroupLock.None, Ct))
            .ShouldBeNull();
    }

    [Theory]
    [InlineData(GroupLock.Share, true)]
    [InlineData(GroupLock.Update, true)]
    [InlineData(GroupLock.None, false)]
    public async Task Should_LockTheGroupRow_When_LinkScopeRequestsLock(GroupLock requested, bool blocks)
    {
        var (ownerId, group) = await NewGroupAsync();
        var token = await ShareAsync(group.Id);

        await using var holderScope = factory.Services.CreateAsyncScope();
        var holderDb = holderScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var holder = await holderDb.Database.BeginTransactionAsync(Ct);
        (await new GroupAccess(holderDb, new FixedUser(ownerId)).OpenOwnedAsync(group.Id, GroupLock.Update, Ct))
            .ShouldNotBeNull();

        await using var waiterScope = factory.Services.CreateAsyncScope();
        var waiterDb = waiterScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var waiter = await waiterDb.Database.BeginTransactionAsync(Ct);
        await waiterDb.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '300ms'", Ct);

        var blocked = false;
        try
        {
            (await new GroupAccess(waiterDb, new AnonymousUser()).OpenAsync(GroupScope.Link(token, null), requested, Ct))
                .ShouldNotBeNull();
        }
        catch (InvalidOperationException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.LockNotAvailable })
        {
            blocked = true;
        }

        blocked.ShouldBe(blocks);
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

    private async Task<string> ShareAsync(Guid groupId)
    {
        var token = ShareTokens.New();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var group = await db.Groups.SingleAsync(row => row.Id == groupId, Ct);
        group.TurnOnShareLink(token);
        await db.SaveChangesAsync(Ct);
        return token;
    }

    private sealed class FixedUser(Guid id) : ICurrentUser
    {
        public Guid Id => id;
    }

    private sealed class AnonymousUser : ICurrentUser
    {
        public Guid Id => throw new UnauthorizedAccessException("A link scope must not read the current user.");
    }
}
