using MediatR;
using Mita.Api.Common;

namespace Mita.Api.Features.GroupInvites.ListGroupInvites;

public sealed record ListGroupInvitesQuery(int Page, int PageSize) : IRequest<Result<PageResponse<GroupInviteDto>>>;
