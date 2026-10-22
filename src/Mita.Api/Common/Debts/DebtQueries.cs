using Mita.Api.Common.Money;
using Mita.Api.Common.Wire;
using Mita.Domain.Debts;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Debts;

public static class DebtQueries
{
    private static readonly string OpenWire = DebtWire.ToWire(DebtStatus.Open);
    private static readonly string SettledWire = DebtWire.ToWire(DebtStatus.Settled);

    public static IQueryable<DebtDto> Project(this IQueryable<Debt> debts, AppDbContext db, DateOnly today) =>
        debts.Select(debt => new DebtDto(
            debt.Id,
            DebtWire.ToWire(debt.Direction),
            debt.Counterparty,
            debt.Amount,
            CurrencyWire.ToWire(debt.Currency),
            debt.Repaid,
            debt.Amount - debt.Repaid,
            debt.Repaid == debt.Amount ? SettledWire : OpenWire,
            debt.Repaid < debt.Amount && debt.DueDate != null && debt.DueDate < today,
            IsoDate.ToWire(debt.Date),
            debt.DueDate == null ? null : IsoDate.ToWire(debt.DueDate.Value),
            debt.AccountId,
            debt.AccountAmount,
            (double?)debt.Rate,
            debt.Note,
            db.DebtRepayments.Count(repayment => repayment.DebtId == debt.Id)));
}
