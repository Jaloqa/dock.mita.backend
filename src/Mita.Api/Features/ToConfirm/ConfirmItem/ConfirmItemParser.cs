using System.Text.Json;
using Mita.Api.Common.Groups;
using Mita.Api.Common.Rates;
using Mita.Api.Common.Tags;
using Mita.Api.Common.Transactions;
using Mita.Api.Common.Wire;
using Mita.Domain.Categories;
using Mita.Domain.Transactions;

namespace Mita.Api.Features.ToConfirm.ConfirmItem;

public static class ConfirmItemParser
{
    private const string Required = "validation.required";
    private const string InvalidValue = "validation.invalidValue";
    private const string NameTooLong = "validation.nameTooLong";
    private const string TitleTooLong = "validation.titleTooLong";
    private const string CategoryIdField = "categoryId";
    private const string NewCategoryField = "newCategory";
    private const string TitleField = "title";

    public static ParsedConfirmation Parse(ConfirmItemCommand command)
    {
        if (!JsonValues.TryGetGuid(command.Version, out var version) || LineErrors(command.Lines).Any())
        {
            throw new InvalidOperationException("The confirmation reached the handler without passing validation.");
        }

        Guid? accountId = JsonValues.TryGetGuid(command.AccountId, out var account) ? account : null;
        decimal? manualRate = ManualRate.TryParse(command.Rate, out var rate) ? rate : null;
        IReadOnlyList<ParsedConfirmLine>? lines = command.Lines is { ValueKind: JsonValueKind.Array } array
            ? [.. array.EnumerateArray().Select(ParseLine)]
            : null;
        return new ParsedConfirmation(version, accountId, manualRate, lines);
    }

    public static IEnumerable<(string Field, string Code)> LineErrors(JsonElement? lines)
    {
        if (JsonValues.IsMissing(lines))
        {
            return [];
        }

        if (lines is not { ValueKind: JsonValueKind.Array } array
            || array.EnumerateArray().Any(line => line.ValueKind != JsonValueKind.Object))
        {
            return [(nameof(ConfirmItemCommand.Lines), InvalidValue)];
        }

        return array.EnumerateArray().SelectMany(ErrorsOf);
    }

    private static IEnumerable<(string Field, string Code)> ErrorsOf(JsonElement line, int index)
    {
        var categoryId = Property(line, CategoryIdField);
        var newCategory = Property(line, NewCategoryField);
        var title = Property(line, TitleField);
        var hasCategory = !JsonValues.IsMissing(categoryId);
        var hasNewCategory = !JsonValues.IsMissing(newCategory);

        if (hasCategory == hasNewCategory)
        {
            yield return (Field(index, CategoryIdField), hasCategory ? InvalidValue : Required);
        }
        else if (hasCategory && !JsonValues.TryGetGuid(categoryId, out _))
        {
            yield return (Field(index, CategoryIdField), TransactionReferenceRules.UnknownCategory);
        }

        if (hasNewCategory && NewCategoryError(newCategory) is { } newCategoryError)
        {
            yield return (Field(index, NewCategoryField), newCategoryError);
        }

        if (!JsonValues.IsMissing(title) && TitleError(title) is { } titleError)
        {
            yield return (Field(index, TitleField), titleError);
        }

        if (TagNames.Error(Property(line, TagNames.Field)) is { } tagsError)
        {
            yield return (Field(index, TagNames.Field), tagsError);
        }
    }

    private static string? NewCategoryError(JsonElement? value)
    {
        if (!JsonValues.TryGetString(value, out var text))
        {
            return InvalidValue;
        }

        var trimmed = text.Trim();
        return trimmed.Length > Category.NameMaxLength ? NameTooLong : trimmed.Any(char.IsControl) ? InvalidValue : null;
    }

    private static string? TitleError(JsonElement? value)
    {
        if (!JsonValues.TryGetString(value, out var text))
        {
            return InvalidValue;
        }

        return text.Trim().Length > Transaction.TitleMaxLength
            ? TitleTooLong
            : JsonValues.TryGetOptionalText(value, out _) ? null : InvalidValue;
    }

    private static ParsedConfirmLine ParseLine(JsonElement line)
    {
        Guid? categoryId = JsonValues.TryGetGuid(Property(line, CategoryIdField), out var id) ? id : null;
        var newCategory = categoryId is null && JsonValues.TryGetString(Property(line, NewCategoryField), out var name)
            ? name.Trim()
            : null;
        JsonValues.TryGetOptionalText(Property(line, TitleField), out var title);
        return new ParsedConfirmLine(categoryId, newCategory, title ?? string.Empty, TagNames.Parse(Property(line, TagNames.Field)));
    }

    private static JsonElement? Property(JsonElement line, string name) =>
        line.TryGetProperty(name, out var value) ? value : null;

    private static string Field(int index, string name) => $"lines.{index}.{name}";
}
