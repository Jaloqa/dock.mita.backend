namespace Mita.Api.Features.Reports.GetReport;

public sealed record ReportPeriodResponse(string Period, string From, string To, long Income, long Expense);
