using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Mita.Api.IntegrationTests.Infrastructure;

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
        string? rawSub = null)
    {
        var claims = new Dictionary<string, object>();
        if (includeSub)
        {
            claims[JwtRegisteredClaimNames.Sub] = rawSub ?? userId.ToString();
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
}
