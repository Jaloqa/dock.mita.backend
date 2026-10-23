using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Mita.Domain.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Groups;

public static class GroupInviteEmails
{
    public static string Normalize(string email) => email.Normalize().Trim().ToLowerInvariant();

    public static bool TryNormalize(string email, [NotNullWhen(true)] out string? normalized)
    {
        try
        {
            normalized = Normalize(email);
            return true;
        }
        catch (ArgumentException)
        {
            normalized = null;
            return false;
        }
    }

    public static async Task<string?> CurrentAsync(this AppDbContext db, Guid userId, CancellationToken ct)
    {
        var account = await db.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new { user.Email, user.NormalizedEmail })
            .SingleAsync(ct);

        var email = Normalize(account.Email ?? throw new InvalidOperationException("Every account has an email."));
        return string.Equals(email.ToUpperInvariant(), account.NormalizedEmail, StringComparison.Ordinal) ? email : null;
    }

    public static async Task<GroupMember?> LockInvitationAsync(
        this AppDbContext db,
        Guid userId,
        Guid memberId,
        CancellationToken ct)
    {
        var lockedGroupIds = await db.Database.SqlQuery<Guid>(
                $"""
                SELECT g."Id" AS "Value" FROM "groups" AS g
                WHERE g."Id" = (SELECT m."GroupId" FROM group_members AS m WHERE m."Id" = {memberId})
                FOR UPDATE
                """)
            .ToListAsync(ct);
        if (lockedGroupIds is not [var groupId])
        {
            return null;
        }

        if (await db.CurrentAsync(userId, ct) is not { } email)
        {
            return null;
        }

        var member = await db.MemberOfGroupAsync(groupId, memberId, ct);
        return member is { IsLinked: false } && member.InvitedEmail == email ? member : null;
    }
}
