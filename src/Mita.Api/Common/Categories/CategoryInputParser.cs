using System.Text.Json;
using Mita.Api.Common.Wire;
using Mita.Domain.Categories;

namespace Mita.Api.Common.Categories;

public sealed record ParsedCategory(string Name, CategoryKind Kind, CategoryColor Color);

public static class CategoryInputParser
{
    public static ParsedCategory Parse(ICategoryInput input)
    {
        if (!TryParseName(input.Name, out var name)
            || !CategoryWire.TryParse(input.Kind, out CategoryKind kind)
            || !CategoryWire.TryParse(input.Color, out CategoryColor color))
        {
            throw new InvalidOperationException("The category input reached the handler without passing validation.");
        }

        return new ParsedCategory(name, kind, color);
    }

    public static bool TryParseName(JsonElement? element, out string name)
    {
        name = string.Empty;
        if (JsonValues.IsMissing(element) || !JsonValues.TryGetString(element, out var text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.Length > Category.NameMaxLength || trimmed.Any(char.IsControl))
        {
            return false;
        }

        name = trimmed;
        return true;
    }
}
