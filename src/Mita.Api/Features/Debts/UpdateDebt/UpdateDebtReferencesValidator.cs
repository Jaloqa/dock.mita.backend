using Mita.Api.Authentication;
using Mita.Api.Common.Debts;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Debts.UpdateDebt;

public sealed class UpdateDebtReferencesValidator(AppDbContext db, ICurrentUser currentUser)
    : DebtReferencesValidator<UpdateDebtCommand>(db, currentUser);
