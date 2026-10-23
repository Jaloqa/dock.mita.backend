using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.CreateExpense;

public sealed class CreateExpenseValidator(TimeProvider time) : ExpenseInputValidator<CreateExpenseCommand>(time);
