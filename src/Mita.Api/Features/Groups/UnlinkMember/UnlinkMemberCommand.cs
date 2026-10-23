using MediatR;
using Mita.Api.Common;

namespace Mita.Api.Features.Groups.UnlinkMember;

public sealed record UnlinkMemberCommand(Guid GroupId, Guid MemberId) : IRequest<Result>;
