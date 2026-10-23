using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Groups.UnshareGroup;

public sealed class UnshareGroupHandler(AppDbContext db, GroupAccess access)
    : IRequestHandler<UnshareGroupCommand, Result>
{
    public async Task<Result> Handle(UnshareGroupCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var context = await access.OpenOwnedAsync(command.Id, GroupLock.Update, cancellationToken);
        if (context is null)
        {
            return Error.NotFound();
        }

        var group = await db.Groups.SingleAsync(candidate => candidate.Id == context.GroupId, cancellationToken);
        group.TurnOffShareLink();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
