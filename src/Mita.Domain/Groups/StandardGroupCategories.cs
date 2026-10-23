using Mita.Domain.Categories;

namespace Mita.Domain.Groups;

public static class StandardGroupCategories
{
    public const string Default = "other";

    private static readonly StarterCategory[] Starters = StarterCategories.All
        .Where(starter => starter.Kind == CategoryKind.Expense && starter.System != SystemCategory.Transfer)
        .ToArray();

    public static IReadOnlyList<string> Keys { get; } = Starters.Select(starter => starter.Key).ToArray();

    public static IEnumerable<string> Names { get; } = Starters
        .SelectMany(starter => starter.Names.Values)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    public static bool IsStandard(string? key) => key is not null && Keys.Contains(key, StringComparer.Ordinal);
}
