using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Accounts;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Accounts.UpdateAccount;

public sealed class UpdateAccountHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<UpdateAccountCommand, Result<AccountDto>>
{
    public async Task<Result<AccountDto>> Handle(UpdateAccountCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var input = AccountInputParser.Parse(command);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var locked = await db.Accounts
            .FromSql($"""SELECT * FROM accounts WHERE "Id" = {command.Id} AND "UserId" = {userId} FOR UPDATE""")
            .ToListAsync(cancellationToken);
        var account = locked.SingleOrDefault();
        if (account is null)
        {
            return Error.NotFound();
        }

        if (account.Currency != input.Currency
            && await db.AccountMovements.AnyAsync(movement => movement.AccountId == account.Id, cancellationToken))
        {
            return Error.Validation(new Dictionary<string, string[]> { ["currency"] = ["validation.currencyLocked"] });
        }

        account.Update(
            input.Name,
            input.Type,
            input.Currency,
            input.OpeningBalance,
            input.CardLast4,
            input.Archived ?? account.Archived);

        await db.SaveChangesAsync(cancellationToken);
        var response = await db.ProjectAccount(userId, account.Id).SingleAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return response;
    }
}
