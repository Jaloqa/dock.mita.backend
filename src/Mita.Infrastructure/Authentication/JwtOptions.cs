using System.Text;
using Microsoft.Extensions.Options;

namespace Mita.Infrastructure.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumSigningKeyBytes = 32;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;
}

public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add($"{JwtOptions.SectionName}:{nameof(JwtOptions.Issuer)} must be configured.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add($"{JwtOptions.SectionName}:{nameof(JwtOptions.Audience)} must be configured.");
        }

        if (Encoding.UTF8.GetByteCount(options.SigningKey) < JwtOptions.MinimumSigningKeyBytes)
        {
            failures.Add(
                $"{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKey)} must be at least {JwtOptions.MinimumSigningKeyBytes} bytes.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
