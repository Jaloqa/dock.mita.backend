using Mita.Api.Common.Debts;

namespace Mita.Api.Features.Overview.GetOverview;

public sealed record OverviewResponse(
    string Range,
    string From,
    string To,
    string PreviousFrom,
    string PreviousTo,
    string Currency,
    OverviewBalanceResponse Balance,
    DebtSummaryDto Debts,
    OverviewTotalResponse Income,
    OverviewTotalResponse Expense,
    long? AverageExpense,
    int TransactionCount,
    IReadOnlyList<SpendingSliceResponse> Spending,
    OverviewMonthResponse Month,
    IReadOnlyList<OverviewBudgetResponse> TopBudgets,
    IReadOnlyList<OverviewTransactionResponse> Recent,
    IReadOnlyList<OverviewGoalResponse> Goals,
    int GoalCount,
    bool HasTransactions);
