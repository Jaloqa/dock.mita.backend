using FluentValidation;
using Mita.Api.Common;

namespace Mita.Api.Features.Goals.ListGoals;

public sealed class ListGoalsValidator : AbstractValidator<ListGoalsQuery>
{
    public ListGoalsValidator() => this.RuleForPaging(query => query.Page, query => query.PageSize);
}
