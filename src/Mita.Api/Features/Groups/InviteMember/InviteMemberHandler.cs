using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Common.Wire;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Groups.InviteMember;

public sealed class InviteMemberHandler(AppDbContext db, GroupAccess access, TimeProvider time)
    : IRequestHandler<InviteMemberCommand, Result<GroupMemberDto>>
{
    public async Task<Result<GroupMemberDto>> Handle(InviteMemberCommand command, CancellationToken cancellationToken)
    {
        var email = ParseEmail(command.Email);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var context = await access.OpenOwnedAsync(command.GroupId, GroupLock.Update, cancellationToken);
        if (context is null)
        {
            return Error.NotFound();
        }

        var member = await db.GroupMembers.SingleOrDefaultAsync(
            candidate => candidate.Id == command.MemberId && candidate.GroupId == context.GroupId,
            cancellationToken);
        if (member is null)
        {
            return Error.NotFound();
        }

        var errors = new Dictionary<string, string[]>();
        if (member.IsLinked)
        {
            errors["memberId"] = [GroupLinkRules.MemberLinked];
        }

        var ownerEmail = await db.Users.AsNoTracking()
            .Where(user => user.Id == context.OwnerId)
            .Select(user => user.Email)
            .FirstOrDefaultAsync(cancellationToken);
        if (ownerEmail is not null && string.Equals(Normalize(ownerEmail), email, StringComparison.Ordinal))
        {
            errors["email"] = [GroupLinkRules.InviteSelf];
        }
        else if (await db.GroupMembers.AnyAsync(
                     other => other.GroupId == context.GroupId && other.Id != member.Id && other.InvitedEmail == email,
                     cancellationToken))
        {
            errors["email"] = [GroupLinkRules.AlreadyInvited];
        }

        if (errors.Count > 0)
        {
            return Error.Validation(errors);
        }

        member.Invite(email, time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        var invited = await db.MemberDtoAsync(context, member.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return invited;
    }

    private static string ParseEmail(JsonElement? value) =>
        JsonValues.TryGetString(value, out var text)
            ? Normalize(text)
            : throw new InvalidOperationException("The email reached the handler without passing validation.");

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
