using Mita.Api.Common.Users;
using Mita.Infrastructure.Authentication;

namespace Mita.Api.Features.Auth.Register;

public sealed record RegisterResponse(UserDto User, IssuedTokens Tokens);
