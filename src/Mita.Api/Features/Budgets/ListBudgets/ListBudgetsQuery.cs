using MediatR;
using Mita.Api.Common;

namespace Mita.Api.Features.Budgets.ListBudgets;

public sealed record ListBudgetsQuery(string? Period, int Page, int PageSize) : IRequest<Result<BudgetPageResponse>>;
