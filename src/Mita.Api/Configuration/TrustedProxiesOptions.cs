using System.Net;
using Microsoft.Extensions.Options;

namespace Mita.Api.Configuration;

public sealed class TrustedProxiesOptions
{
    public const string SectionName = "ForwardedHeaders";

    public string[] KnownProxies { get; set; } = [];

    public string[] KnownNetworks { get; set; } = [];

    public int ForwardLimit { get; set; } = 1;
}

public sealed class TrustedProxiesOptionsValidator : IValidateOptions<TrustedProxiesOptions>
{
    public ValidateOptionsResult Validate(string? name, TrustedProxiesOptions options)
    {
        var failures = new List<string>();

        var invalidProxies = options.KnownProxies.Where(proxy => !IPAddress.TryParse(proxy, out _)).ToList();
        if (invalidProxies.Count > 0)
        {
            failures.Add(
                $"{TrustedProxiesOptions.SectionName}:{nameof(TrustedProxiesOptions.KnownProxies)} contains invalid IP addresses: {string.Join(", ", invalidProxies)}.");
        }

        var invalidNetworks = options.KnownNetworks.Where(network => !IPNetwork.TryParse(network, out _)).ToList();
        if (invalidNetworks.Count > 0)
        {
            failures.Add(
                $"{TrustedProxiesOptions.SectionName}:{nameof(TrustedProxiesOptions.KnownNetworks)} contains invalid CIDR networks: {string.Join(", ", invalidNetworks)}.");
        }

        if (options.ForwardLimit is < 1 or > 10)
        {
            failures.Add(
                $"{TrustedProxiesOptions.SectionName}:{nameof(TrustedProxiesOptions.ForwardLimit)} must be between 1 and 10.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
