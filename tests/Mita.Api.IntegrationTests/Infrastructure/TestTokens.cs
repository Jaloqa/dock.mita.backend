using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Mita.Infrastructure.Authentication;
using Mita.Infrastructure.Identity;
using Shouldly;

namespace Mita.Api.IntegrationTests.Infrastructure;

public sealed record TestUserToken(Guid UserId, string Token);

public static class TestTokens
{
    public const string Issuer = "mita-tests";
    public const string Audience = "mita-api-tests";
    public const string SigningKey = "test-only-signing-key-0123456789-abcdef";

    public static string Create(
        Guid userId,
        DateTimeOffset? expires = null,
        string? signingKey = null,
        bool includeSub = true,
        string? issuer = null,
        string? audience = null,
        string? rawSub = null,
        string? stamp = null)
    {
        var claims = new Dictionary<string, object>();
        if (includeSub)
        {
            claims[JwtRegisteredClaimNames.Sub] = rawSub ?? userId.ToString();
        }

        if (stamp is not null)
        {
            claims[TokenService.StampClaim] = stamp;
        }

        var lifetime = expires ?? DateTimeOffset.UtcNow.AddMinutes(10);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer ?? Issuer,
            Audience = audience ?? Audience,
            Claims = claims,
            NotBefore = lifetime.UtcDateTime.AddHours(-1),
            Expires = lifetime.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey ?? SigningKey)),
                SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public static async Task<TestUserToken> CreateForNewUserAsync(WebApplicationFactory<Program> app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var email = $"{Guid.NewGuid():N}@example.com";
        var user = new AppUser { Id = Guid.NewGuid(), UserName = email, Email = email, Name = "Test" };

        var created = await users.CreateAsync(user);
        created.Succeeded.ShouldBeTrue();

        return new TestUserToken(user.Id, Create(user.Id, stamp: user.SecurityStamp));
    }
}
