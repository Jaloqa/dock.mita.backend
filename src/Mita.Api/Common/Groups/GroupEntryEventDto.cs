using System.Text.Json.Serialization;
using Mita.Api.Common.Json;

namespace Mita.Api.Common.Groups;

public sealed record GroupEntryEventDto<TState>(
    Guid Id,
    string Action,
    [property: JsonConverter(typeof(UtcIsoDateTimeOffsetConverter))] DateTimeOffset At,
    GroupActorDto Actor,
    TState State);
