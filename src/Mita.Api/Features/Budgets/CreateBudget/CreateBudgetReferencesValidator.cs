using Mita.Api.Authentication;
using Mita.Api.Common.Budgets;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Budgets.CreateBudget;

public sealed class CreateBudgetReferencesValidator(AppDbContext db, ICurrentUser currentUser)
    : BudgetReferencesValidator<CreateBudgetCommand>(db, currentUser);
