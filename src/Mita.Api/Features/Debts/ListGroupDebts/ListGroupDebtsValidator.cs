using FluentValidation;
using Mita.Api.Common;

namespace Mita.Api.Features.Debts.ListGroupDebts;

public sealed class ListGroupDebtsValidator : AbstractValidator<ListGroupDebtsQuery>
{
    public ListGroupDebtsValidator() => this.RuleForPaging(query => query.Page, query => query.PageSize);
}
