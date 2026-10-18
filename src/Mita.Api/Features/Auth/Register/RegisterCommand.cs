using System.Text;
using MediatR;
using Mita.Api.Common;
using Mita.Domain.Users;

namespace Mita.Api.Features.Auth.Register;

public sealed record RegisterRequest(string? Name, string? Email, string? Password);

public sealed record RegisterCommand(string? Name, string? Email, string? Password, Locale Locale)
    : IRequest<Result<RegisterResponse>>
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Email = ").Append(Email).Append(", Locale = ").Append(Locale);
        return true;
    }
}
