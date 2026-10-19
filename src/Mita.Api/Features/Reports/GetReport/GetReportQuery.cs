using MediatR;
using Mita.Api.Common;

namespace Mita.Api.Features.Reports.GetReport;

public sealed record GetReportQuery(string? Range) : IRequest<Result<ReportResponse>>;
