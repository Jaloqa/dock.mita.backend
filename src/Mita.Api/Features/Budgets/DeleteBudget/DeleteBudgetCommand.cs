using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Caching;
using Mita.Api.Common.Categories;

namespace Mita.Api.Features.Budgets.DeleteBudget;

public sealed record DeleteBudgetCommand(Guid Id) : IRequest<Result>, IInvalidatesCache
{
    public IReadOnlyCollection<string> CacheTagsToInvalidate => [CategoryCacheTags.Categories];
}
