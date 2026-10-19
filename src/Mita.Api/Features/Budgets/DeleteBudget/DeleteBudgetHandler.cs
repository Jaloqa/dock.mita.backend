using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Budgets.DeleteBudget;

public sealed class DeleteBudgetHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<DeleteBudgetCommand, Result>
{
    public async Task<Result> Handle(DeleteBudgetCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var deleted = await db.Budgets
            .Where(candidate => candidate.Id == command.Id && candidate.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted == 0 ? Error.NotFound() : Result.Success();
    }
}
