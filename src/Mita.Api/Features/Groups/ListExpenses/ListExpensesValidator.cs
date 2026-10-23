using FluentValidation;
using Mita.Api.Common;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.ListExpenses;

public sealed class ListExpensesValidator : AbstractValidator<ListExpensesQuery>
{
    public ListExpensesValidator()
    {
        this.RuleForPaging(query => query.Page, query => query.PageSize);

        RuleFor(query => query.Deleted)
            .Must(deleted => GroupWire.TryParseDeletedFlag(deleted, out _))
            .WithErrorCode("validation.invalidValue");
    }
}
