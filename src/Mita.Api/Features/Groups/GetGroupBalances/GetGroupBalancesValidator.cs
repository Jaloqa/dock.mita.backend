using FluentValidation;
using Mita.Api.Common.Money;

namespace Mita.Api.Features.Groups.GetGroupBalances;

public sealed class GetGroupBalancesValidator : AbstractValidator<GetGroupBalancesQuery>
{
    public GetGroupBalancesValidator()
    {
        RuleFor(query => query.Currency)
            .Must(currency => CurrencyWire.TryParse(currency, out _))
            .When(query => query.Currency is not null)
            .WithErrorCode("validation.invalidValue");
    }
}
