using Microsoft.Net.Http.Headers;
using Mita.Domain.Users;

namespace Mita.Api.Features.Auth.Register;

public static class AcceptLanguageLocale
{
    private static readonly Dictionary<string, Locale> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = Locale.En,
        ["pl"] = Locale.Pl,
        ["ru"] = Locale.Ru,
        ["uk"] = Locale.Uk
    };

    public static Locale Resolve(IEnumerable<StringWithQualityHeaderValue> acceptLanguage)
    {
        var match = acceptLanguage
            .Select((tag, index) => (Tag: tag.Value.ToString(), Quality: tag.Quality ?? 1, Index: index))
            .Where(entry => entry.Quality > 0)
            .OrderByDescending(entry => entry.Quality)
            .ThenBy(entry => entry.Index)
            .Select(entry => Supported.TryGetValue(entry.Tag.Split('-')[0], out var locale) ? locale : (Locale?)null)
            .FirstOrDefault(locale => locale is not null);

        return match ?? Locale.En;
    }
}
