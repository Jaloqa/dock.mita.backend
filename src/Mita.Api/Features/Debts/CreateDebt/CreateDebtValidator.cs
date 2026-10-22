using Mita.Api.Common.Debts;

namespace Mita.Api.Features.Debts.CreateDebt;

public sealed class CreateDebtValidator(TimeProvider time) : DebtInputValidator<CreateDebtCommand>(time);
