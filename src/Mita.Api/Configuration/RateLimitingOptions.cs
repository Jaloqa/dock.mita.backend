using Microsoft.Extensions.Options;

namespace Mita.Api.Configuration;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int AuthPermitLimit { get; set; } = 10;

    public int SessionPermitLimit { get; set; } = 60;
}

public sealed class RateLimitingOptionsValidator : IValidateOptions<RateLimitingOptions>
{
    public ValidateOptionsResult Validate(string? name, RateLimitingOptions options)
    {
        var failures = new List<string>();
        if (options.AuthPermitLimit < 1)
        {
            failures.Add(MustBePositive(nameof(RateLimitingOptions.AuthPermitLimit)));
        }

        if (options.SessionPermitLimit < 1)
        {
            failures.Add(MustBePositive(nameof(RateLimitingOptions.SessionPermitLimit)));
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static string MustBePositive(string key) => $"{RateLimitingOptions.SectionName}:{key} must be at least 1.";
}
