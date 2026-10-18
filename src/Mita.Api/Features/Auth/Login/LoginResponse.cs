using Mita.Api.Common.Users;
using Mita.Infrastructure.Authentication;

namespace Mita.Api.Features.Auth.Login;

public sealed record LoginResponse(UserDto User, IssuedTokens Tokens);
