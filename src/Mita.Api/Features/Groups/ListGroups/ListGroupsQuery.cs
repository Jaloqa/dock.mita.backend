using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.ListGroups;

public sealed record ListGroupsQuery(int Page, int PageSize) : IRequest<Result<PageResponse<GroupSummaryDto>>>;
