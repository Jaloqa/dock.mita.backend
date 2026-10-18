using System.Text;
using MediatR;
using Mita.Api.Common;

namespace Mita.Api.Features.Auth.Refresh;

public sealed record RefreshCommand(string? RefreshToken) : IRequest<Result<RefreshResponse>>
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("HasRefreshToken = ").Append(RefreshToken is not null);
        return true;
    }
}
