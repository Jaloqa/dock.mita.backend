using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.ListExpenses;

public sealed record ListExpensesQuery(GroupScope Scope, string? Deleted, int Page, int PageSize)
    : IRequest<Result<PageResponse<GroupExpenseDto>>>;
