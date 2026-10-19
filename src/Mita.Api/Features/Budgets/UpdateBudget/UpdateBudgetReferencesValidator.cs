using Mita.Api.Authentication;
using Mita.Api.Common.Budgets;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Budgets.UpdateBudget;

public sealed class UpdateBudgetReferencesValidator(AppDbContext db, ICurrentUser currentUser)
    : BudgetReferencesValidator<UpdateBudgetCommand>(db, currentUser);
