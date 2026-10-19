using System.Text.Json;
using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Budgets;
using Mita.Api.Common.Caching;
using Mita.Api.Common.Categories;

namespace Mita.Api.Features.Budgets.CreateBudget;

public sealed record CreateBudgetRequest(JsonElement? CategoryId, JsonElement? Period, JsonElement? Limit);

public sealed record CreateBudgetCommand(JsonElement? CategoryId, JsonElement? Period, JsonElement? Limit)
    : IRequest<Result<BudgetDto>>, IBudgetInput, IInvalidatesCache
{
    public Guid? ExistingId => null;

    public IReadOnlyCollection<string> CacheTagsToInvalidate => [CategoryCacheTags.Categories];
}
