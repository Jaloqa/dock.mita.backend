using System.Text;
using MediatR;
using Mita.Api.Common;

namespace Mita.Api.Features.Auth.Logout;

public sealed record LogoutCommand(string? RefreshToken, Guid? AuthenticatedUserId) : IRequest<Result>
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("AuthenticatedUserId = ").Append(AuthenticatedUserId);
        return true;
    }
}
