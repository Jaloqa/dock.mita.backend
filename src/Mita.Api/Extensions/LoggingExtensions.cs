using Elastic.Apm.NetCoreAll;
using Elastic.CommonSchema.Serilog;
using Mita.Api.Common.Logging;
using Serilog;

namespace Mita.Api.Extensions;

public static class LoggingExtensions
{
    public static WebApplicationBuilder AddMitaLogging(this WebApplicationBuilder builder)
    {
        // Keeping the logger per host stops parallel test hosts from replacing each other's logger through the static Log.Logger.
        builder.Host.UseSerilog(
            (context, services, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.With<SensitiveDataMaskingEnricher>()
                .WriteTo.Console(new EcsTextFormatter()),
            preserveStaticLogger: true);

        if (!string.IsNullOrWhiteSpace(builder.Configuration["ElasticApm:ServerUrl"]))
        {
            builder.Services.AddAllElasticApm();
        }

        return builder;
    }

    public static IApplicationBuilder UseMitaRequestLogging(this IApplicationBuilder app)
    {
        var logger = app.ApplicationServices.GetRequiredService<Serilog.ILogger>();
        return app.UseSerilogRequestLogging(options => options.Logger = logger);
    }
}
