using FluentValidation;
using Mita.Api.Common;
using Mita.Api.Common.Categories;
using Mita.Domain.Categories;

namespace Mita.Api.Features.Categories.ListCategories;

public sealed class ListCategoriesValidator : AbstractValidator<ListCategoriesQuery>
{
    public ListCategoriesValidator()
    {
        this.RuleForPaging(query => query.Page, query => query.PageSize);

        RuleFor(query => query.Kind)
            .Must(kind => CategoryWire.TryParse(kind, out CategoryKind _))
            .When(query => query.Kind is not null)
            .WithErrorCode("validation.invalidValue");
    }
}
