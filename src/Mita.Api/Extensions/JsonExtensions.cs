using Mita.Api.Common.Json;

namespace Mita.Api.Extensions;

public static class JsonExtensions
{
    public static IServiceCollection AddMitaJson(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new MonthStartJsonConverter()));
        return services;
    }
}