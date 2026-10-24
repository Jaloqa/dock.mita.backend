using FluentValidation;
using Mita.Api.Common;

namespace Mita.Api.Features.ToConfirm.ListToConfirm;

public sealed class ListToConfirmValidator : AbstractValidator<ListToConfirmQuery>
{
    public ListToConfirmValidator() => this.RuleForPaging(query => query.Page, query => query.PageSize);
}
