using System.Text.Json;
using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.AddMember;

public sealed record AddMemberRequest(JsonElement? Name);

public sealed record AddMemberCommand(Guid GroupId, JsonElement? Name) : IRequest<Result<GroupMemberDto>>;
