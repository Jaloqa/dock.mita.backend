using MediatR;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Common.Wire;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.ToConfirm.RejectItem;

public sealed class RejectItemHandler(
    AppDbContext db,
    ICurrentUser currentUser,
    GroupRecordWriter writer,
    TimeProvider time)
    : IRequestHandler<RejectItemCommand, Result>
{
    public async Task<Result> Handle(RejectItemCommand command, CancellationToken cancellationToken)
    {
        if (!GroupWire.TryParseEntryPath(command.Kind, out var kind))
        {
            return Error.NotFound();
        }

        if (!JsonValues.TryGetGuid(command.Version, out var version))
        {
            throw new InvalidOperationException("The rejection reached the handler without passing validation.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var locked = await writer.LockAsync(currentUser.Id, kind, command.EntryId, version, cancellationToken);
        if (!locked.IsSuccess)
        {
            return locked.Error!;
        }

        var (item, confirmation) = locked.Value;
        confirmation.Decide(GroupTerms.Canonical(item.Current), time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
