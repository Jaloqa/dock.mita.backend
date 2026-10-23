using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.ExpenseHistory;

public sealed record ExpenseHistoryQuery(GroupScope Scope, Guid ExpenseId, int Page, int PageSize)
    : IRequest<Result<PageResponse<GroupEntryEventDto<GroupExpenseState>>>>;
