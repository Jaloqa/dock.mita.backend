using Mita.Api.Authentication;
using Mita.Api.Common.Debts;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Debts.CreateDebt;

public sealed class CreateDebtReferencesValidator(AppDbContext db, ICurrentUser currentUser)
    : DebtReferencesValidator<CreateDebtCommand>(db, currentUser);
