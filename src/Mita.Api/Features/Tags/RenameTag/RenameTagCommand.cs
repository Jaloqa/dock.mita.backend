using System.Text.Json;
using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Tags;

namespace Mita.Api.Features.Tags.RenameTag;

public sealed record RenameTagRequest(JsonElement? Name);

public sealed record RenameTagCommand(Guid Id, JsonElement? Name) : IRequest<Result<TagDto>>;
