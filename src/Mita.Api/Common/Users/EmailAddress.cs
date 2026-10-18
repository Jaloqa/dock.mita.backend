using System.Text.RegularExpressions;

namespace Mita.Api.Common.Users;

public static partial class EmailAddress
{
    public const int MaxLength = 254;

    public static bool IsValid(string email) =>
        email.Length <= MaxLength && !email.Any(char.IsControl) && Pattern().IsMatch(email);

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex Pattern();
}
