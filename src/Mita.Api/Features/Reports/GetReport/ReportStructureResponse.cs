namespace Mita.Api.Features.Reports.GetReport;

public sealed record ReportStructureResponse(
    string Period,
    string From,
    string To,
    long Total,
    IReadOnlyList<ReportStructureItemResponse> Items);
