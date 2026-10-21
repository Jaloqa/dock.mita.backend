using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Tags;
using Mita.Api.Common.Transactions;
using Mita.Infrastructure.Persistence;
using Npgsql;

namespace Mita.Api.Features.Transactions.UpdateTransaction;

public sealed class UpdateTransactionHandler(AppDbContext db, ICurrentUser currentUser, TransactionWriter writer)
    : IRequestHandler<UpdateTransactionCommand, Result<TransactionDto>>
{
    private const string CategoryForeignKey = "FK_transactions_categories_CategoryId";

    public async Task<Result<TransactionDto>> Handle(UpdateTransactionCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var existingAccountId = await db.Transactions.AsNoTracking()
            .Where(candidate => candidate.Id == command.Id && candidate.UserId == userId)
            .Select(candidate => (Guid?)candidate.AccountId)
            .SingleOrDefaultAsync(cancellationToken);
        if (existingAccountId is null)
        {
            return Error.NotFound();
        }

        var input = TransactionInputParser.Parse(command);

        await using var databaseTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var prepared = await writer.PrepareAsync(userId, input, existingAccountId, cancellationToken);
        if (!prepared.IsSuccess)
        {
            return prepared.Error!;
        }

        var tagIds = await db.UpsertAsync(userId, input.Tags, cancellationToken);

        var locked = await db.Transactions
            .FromSql($"""SELECT * FROM transactions WHERE "Id" = {command.Id} AND "UserId" = {userId} FOR UPDATE""")
            .ToListAsync(cancellationToken);
        var transaction = locked.SingleOrDefault();
        if (transaction is null)
        {
            return Error.NotFound();
        }

        transaction.Update(prepared.Value);
        await db.ReplaceAsync(transaction.Id, tagIds, cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.ForeignKeyViolation,
            ConstraintName: CategoryForeignKey
        })
        {
            return Error.Validation(new Dictionary<string, string[]>
            {
                [TransactionReferenceRules.CategoryField] = [TransactionReferenceRules.UnknownCategory]
            });
        }

        await databaseTransaction.CommitAsync(cancellationToken);

        return await db.Transactions.AsNoTracking()
            .Where(candidate => candidate.Id == command.Id)
            .Select(TransactionQueries.ToDto)
            .SingleAsync(cancellationToken);
    }
}
