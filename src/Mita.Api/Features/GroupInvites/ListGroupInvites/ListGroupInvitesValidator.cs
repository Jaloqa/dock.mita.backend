using FluentValidation;
using Mita.Api.Common;

namespace Mita.Api.Features.GroupInvites.ListGroupInvites;

public sealed class ListGroupInvitesValidator : AbstractValidator<ListGroupInvitesQuery>
{
    public ListGroupInvitesValidator() => this.RuleForPaging(query => query.Page, query => query.PageSize);
}
