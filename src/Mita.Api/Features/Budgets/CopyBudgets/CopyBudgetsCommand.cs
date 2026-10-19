using System.Text.Json;
using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Caching;
using Mita.Api.Common.Categories;

namespace Mita.Api.Features.Budgets.CopyBudgets;

public sealed record CopyBudgetsRequest(JsonElement? Period);

public sealed record CopyBudgetsCommand(JsonElement? Period) : IRequest<Result<BudgetCopyResponse>>, IInvalidatesCache
{
    public IReadOnlyCollection<string> CacheTagsToInvalidate => [CategoryCacheTags.Categories];
}
