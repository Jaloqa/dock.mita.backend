using System.Text.Json;
using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.InviteMember;

public sealed record InviteMemberRequest(JsonElement? Email);

public sealed record InviteMemberCommand(Guid GroupId, Guid MemberId, JsonElement? Email)
    : IRequest<Result<GroupMemberDto>>;
