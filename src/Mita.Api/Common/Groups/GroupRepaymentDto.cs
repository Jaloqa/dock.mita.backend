using System.Text.Json.Serialization;
using Mita.Api.Common.Json;

namespace Mita.Api.Common.Groups;

public sealed record GroupRepaymentDto(
    Guid Id,
    Guid FromMemberId,
    Guid ToMemberId,
    long Amount,
    string Currency,
    string Date,
    double Rate,
    bool RateManual,
    long GroupAmount,
    string? Note,
    bool Deleted,
    GroupActorDto CreatedBy,
    [property: JsonConverter(typeof(UtcIsoDateTimeOffsetConverter))] DateTimeOffset CreatedAt,
    GroupActorDto? UpdatedBy,
    [property: JsonConverter(typeof(UtcIsoDateTimeOffsetConverter))] DateTimeOffset? UpdatedAt);
