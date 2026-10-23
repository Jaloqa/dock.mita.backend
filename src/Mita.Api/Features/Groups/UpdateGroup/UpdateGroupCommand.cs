using System.Text.Json;
using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.UpdateGroup;

public sealed record UpdateGroupRequest(JsonElement? Name, JsonElement? Currency);

public sealed record UpdateGroupCommand(Guid Id, JsonElement? Name, JsonElement? Currency)
    : IRequest<Result<GroupDto>>;
