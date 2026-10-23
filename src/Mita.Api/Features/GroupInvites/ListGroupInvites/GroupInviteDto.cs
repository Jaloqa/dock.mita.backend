using System.Text.Json.Serialization;
using Mita.Api.Common.Json;

namespace Mita.Api.Features.GroupInvites.ListGroupInvites;

public sealed record GroupInviteDto(
    Guid MemberId,
    Guid GroupId,
    string GroupName,
    string MemberName,
    string OwnerName,
    [property: JsonConverter(typeof(UtcIsoDateTimeOffsetConverter))] DateTimeOffset InvitedAt);
