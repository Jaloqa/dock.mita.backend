using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.LinkMember;

public sealed record LinkMemberCommand(Guid GroupId, Guid MemberId) : IRequest<Result<GroupMemberDto>>;
