using FluentValidation;
using Mita.Api.Common;

namespace Mita.Api.Features.Debts.ListRepayments;

public sealed class ListRepaymentsValidator : AbstractValidator<ListRepaymentsQuery>
{
    public ListRepaymentsValidator() => this.RuleForPaging(query => query.Page, query => query.PageSize);
}
