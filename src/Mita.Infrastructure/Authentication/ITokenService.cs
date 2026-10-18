using Mita.Infrastructure.Identity;

namespace Mita.Infrastructure.Authentication;

public interface ITokenService
{
    Task<IssuedTokens> IssueAsync(AppUser user, Guid? familyId, CancellationToken ct);

    Task<RefreshOutcome> RefreshAsync(string refreshToken, CancellationToken ct);

    Task<Guid?> FindOwnerAsync(string refreshToken, CancellationToken ct);

    Task RevokeFamilyAsync(string refreshToken, CancellationToken ct);

    Task RevokeAllAsync(Guid userId, CancellationToken ct);
}

public sealed record IssuedTokens(
    string AccessToken,
    DateTimeOffset AccessExpires,
    string RefreshToken,
    DateTimeOffset RefreshExpires);

public abstract record RefreshOutcome
{
    private RefreshOutcome()
    {
    }

    public sealed record Rotated(IssuedTokens Tokens, AppUser User) : RefreshOutcome;

    public sealed record Invalid : RefreshOutcome;

    public sealed record Superseded : RefreshOutcome;

    public sealed record ReuseDetected(Guid UserId) : RefreshOutcome;
}
