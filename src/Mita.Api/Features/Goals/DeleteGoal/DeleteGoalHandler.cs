using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Goals.DeleteGoal;

public sealed class DeleteGoalHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<DeleteGoalCommand, Result>
{
    public async Task<Result> Handle(DeleteGoalCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var deleted = await db.Goals
            .Where(candidate => candidate.Id == command.Id && candidate.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted == 0 ? Error.NotFound() : Result.Success();
    }
}
