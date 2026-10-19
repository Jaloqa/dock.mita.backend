namespace Mita.Api.Features.Reports.GetReport;

public sealed record ReportStructureItemResponse(Guid CategoryId, string Name, string Color, long Amount, double Percent);
