using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Tags;
using Mita.Api.Common.Transactions;
using Mita.Domain.Transactions;
using Mita.Infrastructure.Persistence;
using Npgsql;

namespace Mita.Api.Features.Transactions.CreateTransaction;

public sealed class CreateTransactionHandler(AppDbContext db, ICurrentUser currentUser, TransactionWriter writer)
    : IRequestHandler<CreateTransactionCommand, Result<TransactionDto>>
{
    private const string CategoryForeignKey = "FK_transactions_categories_CategoryId";

    public async Task<Result<TransactionDto>> Handle(CreateTransactionCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var input = TransactionInputParser.Parse(command);

        await using var databaseTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var prepared = await writer.PrepareAsync(userId, input, null, cancellationToken);
        if (!prepared.IsSuccess)
        {
            return prepared.Error!;
        }

        var tagIds = await db.UpsertAsync(userId, input.Tags, cancellationToken);

        var transaction = Transaction.Create(userId, prepared.Value);
        db.Transactions.Add(transaction);
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
            .Where(candidate => candidate.Id == transaction.Id)
            .Select(TransactionQueries.ToDto)
            .SingleAsync(cancellationToken);
    }
}
