using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.UpdateExpense;

public sealed class UpdateExpenseValidator(TimeProvider time) : ExpenseInputValidator<UpdateExpenseCommand>(time);
