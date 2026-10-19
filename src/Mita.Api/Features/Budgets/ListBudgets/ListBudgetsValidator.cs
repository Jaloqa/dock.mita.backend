using FluentValidation;
using Mita.Api.Common;
using Mita.Domain.Periods;

namespace Mita.Api.Features.Budgets.ListBudgets;

public sealed class ListBudgetsValidator : AbstractValidator<ListBudgetsQuery>
{
    public ListBudgetsValidator()
    {
        this.RuleForPaging(query => query.Page, query => query.PageSize);

        RuleFor(query => query.Period)
            .Must(period => PeriodKey.TryParse(period, out _))
            .When(query => query.Period is not null)
            .WithErrorCode("validation.invalidValue");
    }
}
