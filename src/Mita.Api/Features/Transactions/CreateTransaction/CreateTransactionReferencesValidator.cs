using Mita.Api.Authentication;
using Mita.Api.Common.Transactions;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Transactions.CreateTransaction;

public sealed class CreateTransactionReferencesValidator(AppDbContext db, ICurrentUser currentUser)
    : TransactionReferencesValidator<CreateTransactionCommand>(db, currentUser);
