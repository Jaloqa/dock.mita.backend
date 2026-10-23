using FluentValidation;
using Mita.Api.Common;

namespace Mita.Api.Features.Groups.ListGroups;

public sealed class ListGroupsValidator : AbstractValidator<ListGroupsQuery>
{
    public ListGroupsValidator() => this.RuleForPaging(query => query.Page, query => query.PageSize);
}
