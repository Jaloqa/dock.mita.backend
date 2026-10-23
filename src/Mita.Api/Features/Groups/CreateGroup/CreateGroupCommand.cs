using System.Text.Json;
using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.CreateGroup;

public sealed record CreateGroupRequest(JsonElement? Name, JsonElement? Currency, JsonElement? Members);

public sealed record CreateGroupCommand(JsonElement? Name, JsonElement? Currency, JsonElement? Members)
    : IRequest<Result<GroupDto>>;
