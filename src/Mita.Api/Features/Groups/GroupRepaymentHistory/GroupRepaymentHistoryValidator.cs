using FluentValidation;
using Mita.Api.Common;

namespace Mita.Api.Features.Groups.GroupRepaymentHistory;

public sealed class GroupRepaymentHistoryValidator : AbstractValidator<GroupRepaymentHistoryQuery>
{
    public GroupRepaymentHistoryValidator() => this.RuleForPaging(query => query.Page, query => query.PageSize);
}
