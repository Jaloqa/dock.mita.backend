using FluentValidation;
using Mita.Api.Common;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.ListGroupRepayments;

public sealed class ListGroupRepaymentsValidator : AbstractValidator<ListGroupRepaymentsQuery>
{
    public ListGroupRepaymentsValidator()
    {
        this.RuleForPaging(query => query.Page, query => query.PageSize);

        RuleFor(query => query.Deleted)
            .Must(deleted => GroupWire.TryParseDeletedFlag(deleted, out _))
            .WithErrorCode("validation.invalidValue");
    }
}
