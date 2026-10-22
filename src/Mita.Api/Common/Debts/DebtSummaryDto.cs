namespace Mita.Api.Common.Debts;

public sealed record DebtSummaryDto(long OwedToMe, long OwedByMe, int Open, int Overdue);
