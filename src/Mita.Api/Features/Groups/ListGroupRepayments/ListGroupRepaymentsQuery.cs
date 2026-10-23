using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.ListGroupRepayments;

public sealed record ListGroupRepaymentsQuery(GroupScope Scope, string? Deleted, int Page, int PageSize)
    : IRequest<Result<PageResponse<GroupRepaymentDto>>>;
