using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Domain.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Groups.ShareGroup;

public sealed class ShareGroupHandler(AppDbContext db, GroupAccess access)
    : IRequestHandler<ShareGroupCommand, Result<GroupDto>>
{
    public async Task<Result<GroupDto>> Handle(ShareGroupCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var context = await access.OpenOwnedAsync(command.Id, GroupLock.Update, cancellationToken);
        if (context is null)
        {
            return Error.NotFound();
        }

        var group = await db.Groups.SingleAsync(candidate => candidate.Id == context.GroupId, cancellationToken);
        group.TurnOnShareLink(ShareTokens.New());
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await db.LoadAsync(context, cancellationToken);
    }
}
