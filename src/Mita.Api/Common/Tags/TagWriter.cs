using Microsoft.EntityFrameworkCore;
using Mita.Domain.Tags;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Tags;

public static class TagWriter
{
    public static async Task<IReadOnlyList<Guid>> UpsertAsync(
        this AppDbContext db,
        Guid userId,
        IReadOnlyList<string> names,
        CancellationToken ct)
    {
        if (names.Count == 0)
        {
            return [];
        }

        var ids = names.Select(_ => Guid.CreateVersion7()).ToArray();
        var nameValues = names.ToArray();

        return await db.Database
            .SqlQuery<Guid>($"""
                INSERT INTO tags ("Id", "UserId", "Name", "CreatedAt", "UpdatedAt")
                SELECT n.id, {userId}, n.name, now(), now()
                FROM unnest({ids}::uuid[], {nameValues}::text[]) AS n(id, name)
                ORDER BY lower(n.name)
                ON CONFLICT ("UserId", lower("Name")) DO UPDATE SET "Name" = tags."Name"
                RETURNING "Id" AS "Value"
                """)
            .ToListAsync(ct);
    }

    public static async Task ReplaceAsync(
        this AppDbContext db,
        Guid transactionId,
        IReadOnlyList<Guid> tagIds,
        CancellationToken ct)
    {
        await db.TransactionTags
            .Where(row => row.TransactionId == transactionId)
            .ExecuteDeleteAsync(ct);

        db.TransactionTags.AddRange(tagIds.Select(tagId => TransactionTag.Create(transactionId, tagId)));
    }
}
