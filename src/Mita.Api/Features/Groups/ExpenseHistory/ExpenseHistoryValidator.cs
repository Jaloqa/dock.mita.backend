using FluentValidation;
using Mita.Api.Common;

namespace Mita.Api.Features.Groups.ExpenseHistory;

public sealed class ExpenseHistoryValidator : AbstractValidator<ExpenseHistoryQuery>
{
    public ExpenseHistoryValidator() => this.RuleForPaging(query => query.Page, query => query.PageSize);
}
