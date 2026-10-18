using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Mita.Infrastructure.Identity;
using Mita.Infrastructure.Persistence;

namespace Mita.Infrastructure.Authentication;

public sealed class TokenService(AppDbContext db, IOptions<JwtOptions> jwt, TimeProvider time) : ITokenService
{
    public static readonly TimeSpan AccessLifetime = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(30);

    public static readonly TimeSpan RotationGracePeriod = TimeSpan.FromSeconds(30);

    public const string StampClaim = "stamp";

    private const int RefreshTokenBytes = 32;
    private const int SecurityStampBytes = 20;

    public async Task<IssuedTokens> IssueAsync(AppUser user, Guid? familyId, CancellationToken ct)
    {
        var tokens = Create(user, familyId ?? Guid.NewGuid(), out var entity);
        db.RefreshTokens.Add(entity);
        await db.SaveChangesAsync(ct);
        return tokens;
    }

    public async Task<RefreshOutcome> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var presentedHash = Hash(refreshToken);

        var stored = await db.RefreshTokens.AsNoTracking()
            .FirstOrDefaultAsync(token => token.TokenHash == presentedHash, ct);
        if (stored is null || stored.ExpiresAt <= now)
        {
            return new RefreshOutcome.Invalid();
        }

        if (stored.RevokedAt is not null)
        {
            if (stored.ReplacedByHash is not null && now - stored.RevokedAt.Value <= RotationGracePeriod)
            {
                return new RefreshOutcome.Superseded();
            }

            await RevokeFamilyByIdAsync(stored.FamilyId, now, ct);
            await RotateSecurityStampAsync(stored.UserId, ct);
            return new RefreshOutcome.ReuseDetected(stored.UserId);
        }

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == stored.UserId, ct);
        if (user is null)
        {
            return new RefreshOutcome.Invalid();
        }

        var tokens = Create(user, stored.FamilyId, out var replacement);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var claimed = await db.RefreshTokens
            .Where(token => token.Id == stored.Id && token.RevokedAt == null)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(token => token.RevokedAt, now)
                    .SetProperty(token => token.ReplacedByHash, replacement.TokenHash),
                ct);

        if (claimed == 0)
        {
            await transaction.RollbackAsync(ct);
            return new RefreshOutcome.Superseded();
        }

        db.RefreshTokens.Add(replacement);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new RefreshOutcome.Rotated(tokens, user);
    }

    public async Task<Guid?> FindOwnerAsync(string refreshToken, CancellationToken ct)
    {
        var hash = Hash(refreshToken);
        return await db.RefreshTokens.AsNoTracking()
            .Where(token => token.TokenHash == hash)
            .Select(token => (Guid?)token.UserId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task RevokeFamilyAsync(string refreshToken, CancellationToken ct)
    {
        var hash = Hash(refreshToken);
        var familyId = await db.RefreshTokens.AsNoTracking()
            .Where(token => token.TokenHash == hash)
            .Select(token => (Guid?)token.FamilyId)
            .FirstOrDefaultAsync(ct);

        if (familyId is not null)
        {
            await RevokeFamilyByIdAsync(familyId.Value, time.GetUtcNow(), ct);
        }
    }

    public async Task RevokeAllAsync(Guid userId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        await db.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(token => token.RevokedAt, now), ct);
    }

    private Task<int> RevokeFamilyByIdAsync(Guid familyId, DateTimeOffset now, CancellationToken ct) =>
        db.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(token => token.RevokedAt, now), ct);

    private Task<int> RotateSecurityStampAsync(Guid userId, CancellationToken ct)
    {
        var securityStamp = Convert.ToHexString(RandomNumberGenerator.GetBytes(SecurityStampBytes));
        var concurrencyStamp = Guid.NewGuid().ToString();

        return db.Users
            .Where(user => user.Id == userId)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(user => user.SecurityStamp, securityStamp)
                    .SetProperty(user => user.ConcurrencyStamp, concurrencyStamp),
                ct);
    }

    private IssuedTokens Create(AppUser user, Guid familyId, out RefreshToken entity)
    {
        var now = time.GetUtcNow();
        var accessExpires = now.Add(AccessLifetime);
        var refreshExpires = now.Add(RefreshLifetime);

        var rawRefresh = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(RefreshTokenBytes));
        entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            FamilyId = familyId,
            TokenHash = Hash(rawRefresh),
            CreatedAt = now,
            ExpiresAt = refreshExpires
        };

        return new IssuedTokens(CreateAccessToken(user, now, accessExpires), accessExpires, rawRefresh, refreshExpires);
    }

    private string CreateAccessToken(AppUser user, DateTimeOffset now, DateTimeOffset expires)
    {
        var settings = jwt.Value;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Email] = user.Email ?? string.Empty,
                [JwtRegisteredClaimNames.Name] = user.Name,
                [StampClaim] = user.SecurityStamp ?? string.Empty,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N")
            },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
