using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Infrastructure.Authentication;

namespace Mita.Api.Extensions;

public static class AuthExtensions
{
    public const string UserPolicy = "User";

    public static IServiceCollection AddMitaAuth(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>()
            .Configure<IConfiguration>((options, root) => root.GetSection(JwtOptions.SectionName).Bind(options))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>(ConfigureJwtBearer);

        services.AddAuthorizationBuilder()
            .AddPolicy(UserPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context => CurrentUser.TryReadId(context.User, out _)));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        return services;
    }

    private static void ConfigureJwtBearer(JwtBearerOptions bearer, IOptions<JwtOptions> jwt)
    {
        var settings = jwt.Value;

        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = settings.Issuer,
            ValidateAudience = true,
            ValidAudience = settings.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ValidateIssuerSigningKey = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            RequireSignedTokens = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        bearer.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                if (!CurrentUser.TryReadId(context.Principal, out _))
                {
                    context.Fail("The token has no valid sub claim.");
                }

                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.Headers.WWWAuthenticate = JwtBearerDefaults.AuthenticationScheme;
                await WriteProblemAsync(context.HttpContext, StatusCodes.Status401Unauthorized, "Unauthorized");
            },
            OnForbidden = context =>
                WriteProblemAsync(context.HttpContext, StatusCodes.Status403Forbidden, "Forbidden")
        };
    }

    private static async Task WriteProblemAsync(HttpContext httpContext, int status, string title)
    {
        httpContext.Response.StatusCode = status;

        await ProblemDetailsWriter.WriteAsync(
            httpContext,
            new ProblemDetails { Status = status, Title = title });
    }
}
