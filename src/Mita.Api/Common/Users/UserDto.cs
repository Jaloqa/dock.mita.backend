using System.Linq.Expressions;
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
    public static readonly Expression<Func<AppUser, UserDto>> Projection = user =>
        new UserDto(user.Id, user.Name, user.Email ?? string.Empty, user.CreatedAt);

    private static readonly Func<AppUser, UserDto> Compiled = Projection.Compile();

    public static UserDto From(AppUser user) => Compiled(user);
}
