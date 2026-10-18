using FluentValidation;
using Mita.Api.Common;

namespace Mita.Api.Features.Accounts.ListAccounts;

public sealed class ListAccountsValidator : AbstractValidator<ListAccountsQuery>
{
    private const string InvalidValue = "validation.invalidValue";

    public ListAccountsValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1).WithErrorCode(InvalidValue);
        RuleFor(query => query.Page)
            .Must(_ => false)
            .When(query => PageRequest.Offset(query.Page, query.PageSize) > int.MaxValue)
            .WithErrorCode(InvalidValue);
        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, PageRequest.MaxPageSize)
            .WithErrorCode(InvalidValue);
    }
}
