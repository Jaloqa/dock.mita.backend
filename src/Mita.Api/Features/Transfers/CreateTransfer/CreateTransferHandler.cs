using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Rates;
using Mita.Api.Common.Time;
using Mita.Api.Common.Transfers;
using Mita.Api.Common.Wire;
using Mita.Domain.Accounts;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Transfers.CreateTransfer;

public sealed class CreateTransferHandler(AppDbContext db, ICurrentUser currentUser, TimeProvider time)
    : IRequestHandler<CreateTransferCommand, Result<TransferDto>>
{
    public async Task<Result<TransferDto>> Handle(CreateTransferCommand command, CancellationToken cancellationToken)
    {
        if (!JsonValues.TryGetIsoDate(command.Date, out var date)
            || !JsonValues.TryGetGuid(command.FromAccountId, out var fromId)
            || !JsonValues.TryGetGuid(command.ToAccountId, out var toId)
            || !JsonValues.TryGetMinorAmount(command.FromAmount, out var fromAmount)
            || !JsonValues.TryGetOptionalText(command.Note, out var note))
        {
            throw new InvalidOperationException("The transfer must be validated before the handler runs.");
        }

        decimal? manualRate = ManualRate.TryParse(command.Rate, out var given) ? given : null;
        var userId = currentUser.Id;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        Guid[] ids = [fromId, toId];
        var locked = await db.Accounts
            .FromSql($"""SELECT * FROM accounts WHERE "Id" = ANY({ids}) AND "UserId" = {userId} ORDER BY "Id" FOR UPDATE""")
            .ToListAsync(cancellationToken);

        var accountErrors = CreateTransferFields.AccountErrors(
            fromId,
            toId,
            locked.ToDictionary(account => account.Id, account => account.Archived));
        if (accountErrors.Count > 0)
        {
            return Error.Validation(accountErrors.ToDictionary(error => error.Key, error => new[] { error.Value }));
        }

        var from = locked.Single(account => account.Id == fromId);
        var to = locked.Single(account => account.Id == toId);

        var rate = await db.ResolveAsync(from.Currency, to.Currency, manualRate, date, UtcCalendar.Today(time), cancellationToken);
        if (rate is null)
        {
            return Error.Unavailable("rates.unavailable");
        }

        var transfer = Transfer.Create(userId, date, from, to, fromAmount, rate.Value, note);
        if (transfer.ToAmount <= 0)
        {
            return Error.Validation(new Dictionary<string, string[]> { ["rate"] = ["validation.positiveAmount"] });
        }

        if (transfer.ToAmount > MinorUnits.MaxAmount)
        {
            return Error.Validation(new Dictionary<string, string[]> { ["rate"] = ["validation.invalidValue"] });
        }

        db.Transfers.Add(transfer);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new TransferDto(
            transfer.Id,
            IsoDate.ToWire(transfer.Date),
            transfer.FromAccountId,
            transfer.ToAccountId,
            transfer.FromAmount,
            transfer.ToAmount,
            (double)transfer.Rate,
            transfer.Note);
    }
}
