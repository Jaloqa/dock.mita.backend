using System.Text;
using MediatR;
using Mita.Api.Common;

namespace Mita.Api.Features.Auth.Login;

public sealed record LoginRequest(string? Email, string? Password);

public sealed record LoginCommand(string? Email, string? Password) : IRequest<Result<LoginResponse>>
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Email = ").Append(Email);
        return true;
    }
}
