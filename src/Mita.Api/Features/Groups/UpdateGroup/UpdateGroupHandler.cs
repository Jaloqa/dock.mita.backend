using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Common.Money;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Groups.UpdateGroup;

public sealed class UpdateGroupHandler(AppDbContext db, GroupAccess access)
    : IRequestHandler<UpdateGroupCommand, Result<GroupDto>>
{
    public async Task<Result<GroupDto>> Handle(UpdateGroupCommand command, CancellationToken cancellationToken)
    {
        var name = GroupNames.Parse(command.Name);
        if (!CurrencyWire.TryParse(command.Currency, out var currency))
        {
            throw new InvalidOperationException("The currency reached the handler without passing validation.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var context = await access.OpenOwnedAsync(command.Id, GroupLock.Update, cancellationToken);
        if (context is null)
        {
            return Error.NotFound();
        }

        if (currency != context.Currency && await db.IsCurrencyLockedAsync(context.GroupId, cancellationToken))
        {
            return Error.Validation(new Dictionary<string, string[]> { ["currency"] = ["validation.groupCurrencyLocked"] });
        }

        var group = await db.Groups.SingleAsync(candidate => candidate.Id == context.GroupId, cancellationToken);
        group.Update(name, currency);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await db.LoadAsync(group.Id, cancellationToken);
    }
}
