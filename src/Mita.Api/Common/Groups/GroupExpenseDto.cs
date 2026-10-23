using System.Text.Json.Serialization;
using Mita.Api.Common.Json;

namespace Mita.Api.Common.Groups;

public sealed record GroupExpenseDto(
    Guid Id,
    Guid PayerId,
    long Amount,
    string Currency,
    string Date,
    string CategoryId,
    IReadOnlyList<string> Tags,
    string? Description,
    double Rate,
    bool RateManual,
    long GroupAmount,
    string Split,
    IReadOnlyList<GroupShareDto> Shares,
    bool Deleted,
    GroupActorDto CreatedBy,
    [property: JsonConverter(typeof(UtcIsoDateTimeOffsetConverter))] DateTimeOffset CreatedAt,
    GroupActorDto? UpdatedBy,
    [property: JsonConverter(typeof(UtcIsoDateTimeOffsetConverter))] DateTimeOffset? UpdatedAt);
