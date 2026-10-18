using FluentValidation;
using Mita.Api.Common;

namespace Mita.Api.Features.Accounts.ListAccounts;

public sealed class ListAccountsValidator : AbstractValidator<ListAccountsQuery>
{
    public ListAccountsValidator() => this.RuleForPaging(query => query.Page, query => query.PageSize);
}
