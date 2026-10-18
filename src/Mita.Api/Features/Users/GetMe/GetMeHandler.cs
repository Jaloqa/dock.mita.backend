using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Users;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Users.GetMe;

public sealed class GetMeHandler(AppDbContext db, ICurrentUser currentUser) : IRequestHandler<GetMeQuery, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(GetMeQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var user = await db.Users.AsNoTracking()
            .Where(row => row.Id == userId)
            .Select(UserDto.Projection)
            .FirstOrDefaultAsync(cancellationToken);

        return user is null ? Error.NotFound() : user;
    }
}
