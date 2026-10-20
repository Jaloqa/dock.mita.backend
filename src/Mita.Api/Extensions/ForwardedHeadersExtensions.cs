using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Mita.Api.Configuration;

namespace Mita.Api.Extensions;

public static class ForwardedHeadersExtensions
{
    public static IServiceCollection AddMitaForwardedHeaders(this IServiceCollection services)
    {
        services.AddOptions<TrustedProxiesOptions>()
            .BindConfiguration(TrustedProxiesOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<TrustedProxiesOptions>, TrustedProxiesOptionsValidator>();

        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<TrustedProxiesOptions>>((forwarded, trusted) =>
            {
                forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                forwarded.ForwardLimit = trusted.Value.ForwardLimit;

                var proxies = trusted.Value.KnownProxies;
                var networks = trusted.Value.KnownNetworks;
                if (proxies.Length == 0 && networks.Length == 0)
                {
                    return;
                }

                forwarded.KnownProxies.Clear();
                forwarded.KnownIPNetworks.Clear();
                foreach (var proxy in proxies)
                {
                    forwarded.KnownProxies.Add(IPAddress.Parse(proxy));
                }

                foreach (var network in networks)
                {
                    forwarded.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
                }
            });

        return services;
    }
}
