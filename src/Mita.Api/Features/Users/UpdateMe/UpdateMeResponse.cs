using Mita.Api.Common.Users;
using Mita.Infrastructure.Authentication;

namespace Mita.Api.Features.Users.UpdateMe;

public sealed record UpdateMeResponse(UserDto User, IssuedTokens? Tokens);
