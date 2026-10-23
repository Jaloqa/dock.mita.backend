using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Groups.DeleteGroup;

public sealed class DeleteGroupHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<DeleteGroupCommand, Result>
{
    public async Task<Result> Handle(DeleteGroupCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var deleted = await db.Groups
            .Where(candidate => candidate.Id == command.Id && candidate.OwnerId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted == 0 ? Error.NotFound() : Result.Success();
    }
}
