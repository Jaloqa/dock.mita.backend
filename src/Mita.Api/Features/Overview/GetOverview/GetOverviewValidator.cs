using FluentValidation;

namespace Mita.Api.Features.Overview.GetOverview;

public sealed class GetOverviewValidator : AbstractValidator<GetOverviewQuery>
{
    public GetOverviewValidator()
    {
        RuleFor(query => query.Period)
            .Must(period => OverviewRanges.TryParse(period, out _))
            .When(query => query.Period is not null)
            .WithErrorCode("validation.invalidValue");
    }
}
