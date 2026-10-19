using FluentValidation;

namespace Mita.Api.Features.Reports.GetReport;

public sealed class GetReportValidator : AbstractValidator<GetReportQuery>
{
    public GetReportValidator()
    {
        RuleFor(query => query.Range)
            .Must(range => ReportMath.TryParseRange(range, out _))
            .When(query => query.Range is not null)
            .WithErrorCode("validation.invalidValue");
    }
}
