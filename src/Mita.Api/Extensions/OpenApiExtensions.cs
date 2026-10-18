using Microsoft.OpenApi;
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
            ResolveVersionSegment(document);
            return Task.CompletedTask;
        }));

        return services;
    }

    private static void ResolveVersionSegment(OpenApiDocument document)
    {
        const string template = "{version}";

        foreach (var (path, item) in document.Paths.Where(entry => entry.Key.Contains(template)).ToList())
        {
            document.Paths.Remove(path);
            document.Paths.Add(path.Replace(template, "1"), item);

            foreach (var operation in item.Operations?.Values ?? Enumerable.Empty<OpenApiOperation>())
            {
                operation.Parameters = operation.Parameters?
                    .Where(parameter => parameter.Name != "version")
                    .ToList();
            }
        }
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
