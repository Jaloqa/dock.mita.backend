using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Mita.Api.Authentication;
using Scalar.AspNetCore;

namespace Mita.Api.Extensions;

public static class OpenApiExtensions
{
    public const string DocumentName = "v1";

    private const string CookieScheme = "cookie";
    private const string BearerScheme = "bearer";

    public static IServiceCollection AddMitaOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(DocumentName, options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "Mita API";
                document.Info.Version = DocumentName;
                AddSecuritySchemes(document);
                ResolveVersionSegment(document);
                return Task.CompletedTask;
            });
            options.AddOperationTransformer((operation, context, _) =>
            {
                var metadata = context.Description.ActionDescriptor.EndpointMetadata;
                if (metadata.OfType<IAuthorizeData>().Any(data => data.Policy != AuthExtensions.GroupLinkPolicy)
                    && !metadata.OfType<IAllowAnonymous>().Any())
                {
                    operation.Security =
                    [
                        new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(CookieScheme, context.Document)] = [] },
                        new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(BearerScheme, context.Document)] = [] }
                    ];
                }

                return Task.CompletedTask;
            });
        });

        return services;
    }

    private static void AddSecuritySchemes(OpenApiDocument document)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[CookieScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Name = AuthCookies.AccessToken,
            Description = "Access token cookie set by register, login and refresh. Unsafe methods also need the X-XSRF-TOKEN header."
        };
        document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "The same access token sent in the Authorization header. No XSRF header is needed."
        };
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
