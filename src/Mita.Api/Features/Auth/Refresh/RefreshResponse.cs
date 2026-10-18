using Mita.Infrastructure.Authentication;

namespace Mita.Api.Features.Auth.Refresh;

public sealed record RefreshResponse(IssuedTokens? Tokens);
