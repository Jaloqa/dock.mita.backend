using System.Text.Json.Serialization;
using Mita.Api.Common.Json;
using Mita.Infrastructure.Identity;

namespace Mita.Api.Common.Users;

public sealed record UserDto(
    Guid Id,
    string Name,
    string Email,
    [property: JsonConverter(typeof(UtcIsoDateTimeOffsetConverter))] DateTimeOffset CreatedAt)
{
    public static UserDto From(AppUser user) => new(user.Id, user.Name, user.Email ?? string.Empty, user.CreatedAt);
}
