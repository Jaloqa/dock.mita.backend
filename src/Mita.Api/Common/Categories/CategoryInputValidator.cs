using System.Text.Json;
using FluentValidation;
using Mita.Api.Common.Wire;
using Mita.Domain.Categories;

namespace Mita.Api.Common.Categories;

public abstract class CategoryInputValidator<T> : AbstractValidator<T>
    where T : ICategoryInput
{
    private const string Required = "validation.required";
    private const string InvalidValue = "validation.invalidValue";
    private const string NameTooLong = "validation.nameTooLong";

    protected CategoryInputValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(input => input.Name)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => JsonValues.TryGetString(value, out _)).WithErrorCode(InvalidValue)
            .Must(value => Trimmed(value).Length <= Category.NameMaxLength).WithErrorCode(NameTooLong)
            .Must(value => !Trimmed(value).Any(char.IsControl)).WithErrorCode(InvalidValue);

        RuleFor(input => input.Kind)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => CategoryWire.TryParse(value, out CategoryKind _)).WithErrorCode(InvalidValue);

        RuleFor(input => input.Color)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => CategoryWire.TryParse(value, out CategoryColor _)).WithErrorCode(InvalidValue);
    }

    private static string Trimmed(JsonElement? value) =>
        JsonValues.TryGetString(value, out var text) ? text.Trim() : string.Empty;
}
