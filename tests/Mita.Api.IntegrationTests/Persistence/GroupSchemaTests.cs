using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Groups;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;
using Npgsql;
using Shouldly;

namespace Mita.Api.IntegrationTests.Persistence;

public sealed class GroupSchemaTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_StoreExpense_AndReplaceItsShares()
    {
        var userId = await RegisterAsync();
        var (group, members) = await TestData.AddGroupAsync(
            factory,
            userId,
            "Trip",
            Currency.EUR,
            "Anna",
            "Olya",
            "Max",
            "Kasia");
        var (anna, olya, max, kasia) = (members[0].Id, members[1].Id, members[2].Id, members[3].Id);
        var category = GroupCategory.Create(group.Id, "Beach");
        var details = new ExpenseDetails(
            olya,
            30_000,
            Currency.PLN,
            new DateOnly(2026, 10, 2),
            null,
            category.Id,
            ["Beach", "food"],
            "Dinner",
            0.25m,
            true,
            7_500,
            GroupSplitMode.Exact,
            GroupExpenseSplits.Exact(7_500, [(olya, 10_000), (max, 20_000)], olya));
        var expense = GroupExpense.Create(group.Id, details);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.GroupCategories.Add(category);
            db.GroupExpenses.Add(expense);
            await db.SaveChangesAsync(Ct);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.GroupExpenses.Include(row => row.Shares).SingleAsync(row => row.Id == expense.Id, Ct);
            stored.Tags.ShouldBe(["Beach", "food"]);
            stored.Description.ShouldBe("Dinner");
            stored.Rate.ShouldBe(0.25m);
            stored.RateManual.ShouldBeTrue();
            stored.CategoryId.ShouldBe(category.Id);
            stored.StandardCategory.ShouldBeNull();
            stored.Split.ShouldBe(GroupSplitMode.Exact);
            stored.Shares.Select(share => (share.MemberId, share.Amount, share.GroupAmount))
                .ShouldBe([(olya, 10_000L, 2_500L), (max, 20_000L, 5_000L)], ignoreOrder: true);

            stored.Update(details with
            {
                Amount = 12_000,
                Currency = Currency.EUR,
                StandardCategory = "cafes",
                CategoryId = null,
                Tags = ["food"],
                Rate = 1m,
                RateManual = false,
                GroupAmount = 12_000,
                Split = GroupSplitMode.Equal,
                Shares = GroupExpenseSplits.Equal(12_000, 12_000, [anna, max, kasia], olya)
            });
            await db.SaveChangesAsync(Ct);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.GroupExpenses.AsNoTracking()
                .Include(row => row.Shares)
                .SingleAsync(row => row.Id == expense.Id, Ct);
            stored.Tags.ShouldBe(["food"]);
            stored.StandardCategory.ShouldBe("cafes");
            stored.CategoryId.ShouldBeNull();
            stored.Shares.Select(share => (share.MemberId, share.Amount, share.GroupAmount))
                .ShouldBe([(anna, 4_000L, 4_000L), (max, 4_000L, 4_000L), (kasia, 4_000L, 4_000L)], ignoreOrder: true);
            stored.Shares.ShouldAllBe(share => share.GroupId == group.Id);
        }
    }

    [Fact]
    public async Task Should_DeleteEveryRow_When_GroupDeleted()
    {
        var userId = await RegisterAsync();
        var (group, members) = await TestData.AddGroupAsync(factory, userId);
        var (kept, keptMembers) = await TestData.AddGroupAsync(factory, userId, "Kept");
        var expense = await TestData.AddGroupExpenseAsync(factory, group, members[0], 9_000, members);
        await TestData.AddGroupExpenseAsync(factory, group, members[1], 1_500, members[1..], deleted: true);
        await TestData.AddGroupRepaymentAsync(factory, group, members[2], members[0], 3_000, deleted: true);
        await TestData.AddGroupExpenseAsync(factory, kept, keptMembers[0], 600, keptMembers);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var beach = GroupCategory.Create(group.Id, "Beach");
        db.GroupCategories.Add(beach);
        await db.SaveChangesAsync(Ct);
        (await db.GroupExpenses
            .Where(row => row.Id == expense.Id)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(row => row.CategoryId, beach.Id)
                    .SetProperty(row => row.StandardCategory, (string?)null),
                Ct)).ShouldBe(1);
        db.GroupEntryEvents.Add(GroupEntryEvent.Create(
            group.Id,
            GroupEntryKind.Expense,
            expense.Id,
            GroupEntryAction.Updated,
            DateTimeOffset.UtcNow,
            GroupActor.Member(members[2].Id),
            "{}"));
        await db.SaveChangesAsync(Ct);

        var deleted = await db.Groups.Where(row => row.Id == group.Id).ExecuteDeleteAsync(Ct);

        deleted.ShouldBe(1);
        (await db.Groups.AnyAsync(row => row.Id == group.Id, Ct)).ShouldBeFalse();
        (await db.GroupMembers.AnyAsync(row => row.GroupId == group.Id, Ct)).ShouldBeFalse();
        (await db.GroupCategories.AnyAsync(row => row.GroupId == group.Id, Ct)).ShouldBeFalse();
        (await db.GroupExpenses.AnyAsync(row => row.GroupId == group.Id, Ct)).ShouldBeFalse();
        (await db.GroupExpenseShares.AnyAsync(row => row.GroupId == group.Id, Ct)).ShouldBeFalse();
        (await db.GroupRepayments.AnyAsync(row => row.GroupId == group.Id, Ct)).ShouldBeFalse();
        (await db.GroupEntryEvents.AnyAsync(row => row.GroupId == group.Id, Ct)).ShouldBeFalse();
        (await db.GroupMembers.CountAsync(row => row.GroupId == kept.Id, Ct)).ShouldBe(3);
        (await db.GroupExpenseShares.CountAsync(row => row.GroupId == kept.Id, Ct)).ShouldBe(3);
        (await db.GroupEntryEvents.CountAsync(row => row.GroupId == kept.Id, Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task Should_RejectMemberDelete_When_OnlyADeletedExpenseShareUsesIt()
    {
        var userId = await RegisterAsync();
        var (group, members) = await TestData.AddGroupAsync(factory, userId);
        await TestData.AddGroupExpenseAsync(factory, group, members[0], 9_000, members, deleted: true);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var failure = await Should.ThrowAsync<PostgresException>(
            () => db.GroupMembers.Where(row => row.Id == members[1].Id).ExecuteDeleteAsync(Ct));

        failure.SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
        failure.TableName.ShouldBe("group_expense_shares");
        (await db.GroupMembers.CountAsync(row => row.GroupId == group.Id, Ct)).ShouldBe(3);
    }

    private async Task<Guid> RegisterAsync()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        using var response = await client.Http.GetAsync("/v1/me", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }
}
