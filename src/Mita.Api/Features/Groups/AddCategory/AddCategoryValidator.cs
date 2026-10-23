using FluentValidation;
using FluentValidation.Results;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.AddCategory;

public sealed class AddCategoryValidator : AbstractValidator<AddCategoryCommand>
{
    public AddCategoryValidator() =>
        RuleFor(command => command.Name).Custom((value, context) =>
        {
            if (GroupNames.Error(value) is { } code)
            {
                context.AddFailure(new ValidationFailure(nameof(AddCategoryCommand.Name), code) { ErrorCode = code });
            }
        });
}
