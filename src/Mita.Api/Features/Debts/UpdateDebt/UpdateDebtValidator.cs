using Mita.Api.Common.Debts;

namespace Mita.Api.Features.Debts.UpdateDebt;

public sealed class UpdateDebtValidator(TimeProvider time) : DebtInputValidator<UpdateDebtCommand>(time);
