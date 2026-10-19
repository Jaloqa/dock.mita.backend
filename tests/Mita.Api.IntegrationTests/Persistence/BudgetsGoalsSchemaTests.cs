using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mita.Api.IntegrationTests.Infrastructure;
using Npgsql;
using Shouldly;

namespace Mita.Api.IntegrationTests.Persistence;

public sealed class BudgetsGoalsSchemaTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_RejectDuplicateBudget()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        using var me = await client.Http.GetAsync("/v1/me", Ct);
        var userId = (await me.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
        var category = await TestData.AddCategoryAsync(factory, userId);
        await TestData.AddBudgetAsync(factory, userId, category.Id, "2026-10", 50_000);

        var failure = await Should.ThrowAsync<DbUpdateException>(
            () => TestData.AddBudgetAsync(factory, userId, category.Id, "2026-10", 70_000));

        failure.InnerException.ShouldBeOfType<PostgresException>().ConstraintName
            .ShouldBe("IX_budgets_user_category_period");
    }
}
