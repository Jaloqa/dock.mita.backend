using Microsoft.Extensions.Options;

namespace Mita.Api.Configuration;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int AuthPermitLimit { get; set; } = 10;
}

public sealed class RateLimitingOptionsValidator : IValidateOptions<RateLimitingOptions>
{
    public ValidateOptionsResult Validate(string? name, RateLimitingOptions options) =>
        options.AuthPermitLimit >= 1
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"{RateLimitingOptions.SectionName}:{nameof(RateLimitingOptions.AuthPermitLimit)} must be at least 1.");
}
