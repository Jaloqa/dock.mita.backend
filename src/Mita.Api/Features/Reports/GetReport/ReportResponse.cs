namespace Mita.Api.Features.Reports.GetReport;

public sealed record ReportResponse(
    string Range,
    string From,
    string To,
    string Currency,
    IReadOnlyList<ReportPeriodResponse> Periods,
    ReportTotalsResponse Totals,
    long AverageIncome,
    long AverageExpense,
    double? SavingsRate,
    ReportStructureResponse Structure,
    bool HasTransactions);
