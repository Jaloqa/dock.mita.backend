using Mita.Domain.Common;
using Mita.Domain.Users;

namespace Mita.Domain.Categories;

public sealed class Category : IAuditable
{
    public const int NameMaxLength = 40;
    public const int StandardKeyMaxLength = 20;

    private Category()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public CategoryKind Kind { get; private set; }

    public CategoryColor Color { get; private set; }

    public SystemCategory? System { get; private set; }

    public string? StandardKey { get; private set; }

    public int Position { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public static Category Create(Guid userId, string name, CategoryKind kind, CategoryColor color)
    {
        var category = new Category { Id = Guid.CreateVersion7(), UserId = userId };
        category.Update(name, kind, color);
        return category;
    }

    public static Category Starter(Guid userId, StarterCategory starter, Locale locale, int position)
    {
        var category = Create(userId, starter.Names[locale], starter.Kind, starter.Color);
        category.System = starter.System;
        category.StandardKey = starter.Key;
        category.Position = position;
        return category;
    }

    public void Update(string name, CategoryKind kind, CategoryColor color)
    {
        Name = name;
        Kind = kind;
        Color = color;
    }
}
