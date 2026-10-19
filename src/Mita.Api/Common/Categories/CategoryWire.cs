using System.Text.Json;
using Mita.Domain.Categories;

namespace Mita.Api.Common.Categories;

public static class CategoryWire
{
    public static string ToWire(CategoryKind kind) => kind switch
    {
        CategoryKind.Expense => "expense",
        CategoryKind.Income => "income",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    public static string ToWire(CategoryColor color) => color switch
    {
        CategoryColor.Chart1 => "chart-1",
        CategoryColor.Chart2 => "chart-2",
        CategoryColor.Chart3 => "chart-3",
        CategoryColor.Chart4 => "chart-4",
        CategoryColor.Chart5 => "chart-5",
        CategoryColor.Chart6 => "chart-6",
        _ => throw new ArgumentOutOfRangeException(nameof(color), color, null)
    };

    public static string? ToWire(SystemCategory? system) => system switch
    {
        null => null,
        SystemCategory.Transfer => "transfer",
        SystemCategory.Other => "other",
        _ => throw new ArgumentOutOfRangeException(nameof(system), system, null)
    };

    public static bool TryParse(string? value, out CategoryKind kind)
    {
        (kind, var ok) = value switch
        {
            "expense" => (CategoryKind.Expense, true),
            "income" => (CategoryKind.Income, true),
            _ => (default(CategoryKind), false)
        };
        return ok;
    }

    public static bool TryParse(JsonElement? element, out CategoryKind kind) =>
        TryParse(Text(element), out kind);

    public static bool TryParse(string? value, out CategoryColor color)
    {
        (color, var ok) = value switch
        {
            "chart-1" => (CategoryColor.Chart1, true),
            "chart-2" => (CategoryColor.Chart2, true),
            "chart-3" => (CategoryColor.Chart3, true),
            "chart-4" => (CategoryColor.Chart4, true),
            "chart-5" => (CategoryColor.Chart5, true),
            "chart-6" => (CategoryColor.Chart6, true),
            _ => (default(CategoryColor), false)
        };
        return ok;
    }

    public static bool TryParse(JsonElement? element, out CategoryColor color) =>
        TryParse(Text(element), out color);

    private static string? Text(JsonElement? element) =>
        element is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;
}
