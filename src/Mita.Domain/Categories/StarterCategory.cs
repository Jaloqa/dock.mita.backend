using Mita.Domain.Users;

namespace Mita.Domain.Categories;

public sealed record StarterCategory(
    string Key,
    CategoryKind Kind,
    CategoryColor Color,
    SystemCategory? System,
    IReadOnlyDictionary<Locale, string> Names);
