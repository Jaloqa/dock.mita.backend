using Microsoft.Extensions.Options;

namespace Mita.Api.Configuration;

public sealed class CorsOriginsOptions
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; set; } = [];
}

public sealed class CorsOriginsOptionsValidator : IValidateOptions<CorsOriginsOptions>
{
    public ValidateOptionsResult Validate(string? name, CorsOriginsOptions options)
    {
        var invalid = options.AllowedOrigins
            .Where(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || uri.PathAndQuery != "/"
                || origin.EndsWith('/'))
            .ToList();

        return invalid.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"{CorsOriginsOptions.SectionName}:{nameof(CorsOriginsOptions.AllowedOrigins)} contains invalid origins: {string.Join(", ", invalid)}.");
    }
}
