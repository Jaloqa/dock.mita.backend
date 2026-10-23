using System.Text.Json;
using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.RenameMember;

public sealed record RenameMemberRequest(JsonElement? Name);

public sealed record RenameMemberCommand(Guid GroupId, Guid MemberId, JsonElement? Name)
    : IRequest<Result<GroupMemberDto>>;
