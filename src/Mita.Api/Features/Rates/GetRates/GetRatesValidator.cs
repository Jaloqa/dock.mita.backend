using FluentValidation;
using Mita.Api.Common.Money;
using Mita.Api.Common.Wire;

namespace Mita.Api.Features.Rates.GetRates;

public sealed class GetRatesValidator : AbstractValidator<GetRatesQuery>
{
    private const string InvalidValue = "validation.invalidValue";

    public GetRatesValidator()
    {
        RuleFor(query => query.Date)
            .Must(value => IsoDate.TryParse(value, out _)).WithErrorCode(InvalidValue);

        RuleFor(query => query.From)
            .Must(value => CurrencyWire.TryParse(value, out _)).WithErrorCode(InvalidValue)
            .When(query => query.From is not null);

        RuleFor(query => query.To)
            .Must(value => CurrencyWire.TryParse(value, out _)).WithErrorCode(InvalidValue)
            .When(query => query.To is not null);
    }
}
