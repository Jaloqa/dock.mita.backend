using Elastic.Apm.NetCoreAll;
using Elastic.CommonSchema.Serilog;
using Serilog;

namespace Mita.Api.Extensions;

public static class LoggingExtensions
{
    public static WebApplicationBuilder AddMitaLogging(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console(new EcsTextFormatter()));

        if (!string.IsNullOrWhiteSpace(builder.Configuration["ElasticApm:ServerUrl"]))
        {
            builder.Services.AddAllElasticApm();
        }

        return builder;
    }
}
