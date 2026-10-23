using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Mita.Api.Common.Logging;

public static partial class ShareLinkRedaction
{
    private const string LinkSegment = "/shared/groups/";

    [return: NotNullIfNotNull(nameof(path))]
    public static string? Redact(string? path) =>
        path is not null && path.Contains(LinkSegment, StringComparison.OrdinalIgnoreCase)
            ? Token().Replace(path, SensitiveDataMaskingEnricher.Mask)
            : path;

    public static bool ContainsToken(string? path) =>
        path is not null && path.Contains(LinkSegment, StringComparison.OrdinalIgnoreCase) && Token().IsMatch(path);

    [GeneratedRegex("(?<=/shared/groups/)[^/?]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Token();
}
