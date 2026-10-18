using MediatR;
using Mita.Api.Common;
using Mita.Infrastructure.Authentication;

namespace Mita.Api.Features.Auth.Refresh;

public sealed class RefreshHandler(ITokenService tokens, SecurityStampCache stamps)
    : IRequestHandler<RefreshCommand, Result<RefreshResponse>>
{
    public async Task<Result<RefreshResponse>> Handle(RefreshCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return SessionExpired();
        }

        var outcome = await tokens.RefreshAsync(command.RefreshToken, cancellationToken);

        if (outcome is RefreshOutcome.ReuseDetected reuse)
        {
            await stamps.InvalidateAsync(reuse.UserId);
        }

        return outcome switch
        {
            RefreshOutcome.Rotated rotated => new RefreshResponse(rotated.Tokens),
            RefreshOutcome.Superseded => new RefreshResponse(null),
            _ => SessionExpired()
        };
    }

    private static Error SessionExpired() => Error.Unauthorized("auth.sessionExpired");
}
