using Mita.Api.Authentication;
using Mita.Api.Common.Transactions;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Transactions.UpdateTransaction;

public sealed class UpdateTransactionReferencesValidator(AppDbContext db, ICurrentUser currentUser)
    : TransactionReferencesValidator<UpdateTransactionCommand>(db, currentUser);
