using Microsoft.EntityFrameworkCore;
using Mita.Domain.Tags;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Tags;

// These calls are translated to SQL lower() and strpos(); the culture and StringComparison overloads the analyzers ask for have no SQL translation.
#pragma warning disable CA1304, CA1311, CA1862
public static class TagQueries
{
    public static IQueryable<TagDto> ProjectTags(this AppDbContext db, Guid userId, string? search)
    {
        var needle = search?.Trim().ToLowerInvariant();
        var tags = db.Tags.AsNoTracking().Where(tag => tag.UserId == userId);
        if (!string.IsNullOrEmpty(needle))
        {
            tags = tags.Where(tag => tag.Name.ToLower().Contains(needle));
        }

        return tags.Project(db);
    }

    public static IQueryable<TagDto> ProjectTag(this AppDbContext db, Guid userId, Guid id) =>
        db.Tags.AsNoTracking().Where(tag => tag.UserId == userId && tag.Id == id).Project(db);

    private static IQueryable<TagDto> Project(this IQueryable<Tag> tags, AppDbContext db) =>
        tags
            .OrderBy(tag => tag.Name.ToLower())
            .ThenBy(tag => tag.Id)
            .Select(tag => new TagDto(tag.Id, tag.Name, db.TransactionTags.Count(link => link.TagId == tag.Id)));
}
#pragma warning restore CA1304, CA1311, CA1862
