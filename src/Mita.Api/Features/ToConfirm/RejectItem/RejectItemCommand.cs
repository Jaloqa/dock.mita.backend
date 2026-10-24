using System.Text.Json;
using MediatR;
using Mita.Api.Common;

namespace Mita.Api.Features.ToConfirm.RejectItem;

public sealed record RejectItemRequest(JsonElement? Version);

public sealed record RejectItemCommand(string Kind, Guid EntryId, JsonElement? Version) : IRequest<Result>;
