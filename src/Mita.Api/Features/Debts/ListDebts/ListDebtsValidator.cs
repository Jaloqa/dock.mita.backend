using FluentValidation;
using Mita.Api.Common;
using Mita.Api.Common.Debts;

namespace Mita.Api.Features.Debts.ListDebts;

public sealed class ListDebtsValidator : AbstractValidator<ListDebtsQuery>
{
    public ListDebtsValidator()
    {
        this.RuleForPaging(query => query.Page, query => query.PageSize);

        RuleFor(query => query.Status)
            .Must(status => DebtWire.TryParseStatus(status, out _))
            .When(query => query.Status is not null)
            .WithErrorCode("validation.invalidValue");
    }
}
