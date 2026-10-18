using Microsoft.AspNetCore.Identity;
using Mita.Domain.Common;

namespace Mita.Infrastructure.Identity;

public sealed class AppUser : IdentityUser<Guid>, IAuditable
{
    public const int NameMaxLength = 40;

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
