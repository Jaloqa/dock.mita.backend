using FluentValidation;
using Mita.Api.Common;

namespace Mita.Api.Features.Tags.ListTags;

public sealed class ListTagsValidator : AbstractValidator<ListTagsQuery>
{
    public ListTagsValidator() => this.RuleForPaging(query => query.Page, query => query.PageSize);
}
