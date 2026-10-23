using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;

namespace Mita.Domain.Groups;

public static class ShareTokens
{
    public const int Length = 43;

    private const int ByteCount = 32;

    private static readonly SearchValues<char> Alphabet =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_");

    public static string New() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(ByteCount));

    public static bool IsWellFormed(string? value) =>
        value is { Length: Length } && !value.AsSpan().ContainsAnyExcept(Alphabet);
}
