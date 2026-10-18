using Microsoft.Extensions.Options;

namespace Mita.Infrastructure.Rates;

public sealed class RatesOptions
{
    public const string SectionName = "Rates";
    public const int MaxHistoryDays = 3650;

    public static readonly TimeSpan MinimumSyncInterval = TimeSpan.FromMinutes(1);

    public bool SyncEnabled { get; set; } = true;

    public TimeSpan SyncInterval { get; set; } = TimeSpan.FromHours(6);

    public int HistoryDays { get; set; } = 183;

    public Uri NbpBaseUrl { get; set; } = new("https://api.nbp.pl/api/");
}

public sealed class RatesOptionsValidator : IValidateOptions<RatesOptions>
{
    public ValidateOptionsResult Validate(string? name, RatesOptions options)
    {
        var failures = new List<string>();

        if (options.SyncInterval < RatesOptions.MinimumSyncInterval)
        {
            failures.Add($"{RatesOptions.SectionName}:{nameof(RatesOptions.SyncInterval)} must be at least one minute.");
        }

        if (options.HistoryDays is < 1 or > RatesOptions.MaxHistoryDays)
        {
            failures.Add(
                $"{RatesOptions.SectionName}:{nameof(RatesOptions.HistoryDays)} must be between 1 and {RatesOptions.MaxHistoryDays}.");
        }

        if (!options.NbpBaseUrl.IsAbsoluteUri || options.NbpBaseUrl.Scheme != Uri.UriSchemeHttps)
        {
            failures.Add($"{RatesOptions.SectionName}:{nameof(RatesOptions.NbpBaseUrl)} must be an absolute https URL.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
