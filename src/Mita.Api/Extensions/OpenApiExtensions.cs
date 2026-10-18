using Scalar.AspNetCore;

namespace Mita.Api.Extensions;

public static class OpenApiExtensions
{
    public const string DocumentName = "v1";

    public static IServiceCollection AddMitaOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(DocumentName, options => options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Title = "Mita API";
            document.Info.Version = DocumentName;
            return Task.CompletedTask;
        }));

        return services;
    }

    public static WebApplication MapMitaOpenApi(this WebApplication app)
    {
        app.MapOpenApi().AllowAnonymous();

        if (app.Environment.IsDevelopment())
        {
            app.MapScalarApiReference().AllowAnonymous();
        }

        return app;
    }
}
