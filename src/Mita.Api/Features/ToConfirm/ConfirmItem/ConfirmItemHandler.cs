using MediatR;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.ToConfirm.ConfirmItem;

public sealed class ConfirmItemHandler(
    AppDbContext db,
    ICurrentUser currentUser,
    GroupRecordWriter writer,
    TimeProvider time)
    : IRequestHandler<ConfirmItemCommand, Result>
{
    public async Task<Result> Handle(ConfirmItemCommand command, CancellationToken cancellationToken)
    {
        if (!GroupWire.TryParseEntryPath(command.Kind, out var kind))
        {
            return Error.NotFound();
        }

        var input = ConfirmItemParser.Parse(command);
        var userId = currentUser.Id;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var locked = await writer.LockAsync(userId, kind, command.EntryId, input.Version, cancellationToken);
        if (!locked.IsSuccess)
        {
            return locked.Error!;
        }

        var (item, confirmation) = locked.Value;
        if (item.Change == ToConfirmChange.Removed)
        {
            await writer.RemoveAsync(confirmation, cancellationToken);
            confirmation.Confirm(null, confirmation.AccountId, time.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var booked = await writer.BookAsync(userId, item, input, confirmation, cancellationToken);
            if (!booked.IsSuccess)
            {
                return booked.Error!;
            }
        }

        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
