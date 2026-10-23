using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.GroupRepaymentHistory;

public sealed record GroupRepaymentHistoryQuery(GroupScope Scope, Guid RepaymentId, int Page, int PageSize)
    : IRequest<Result<PageResponse<GroupEntryEventDto<GroupRepaymentState>>>>;
