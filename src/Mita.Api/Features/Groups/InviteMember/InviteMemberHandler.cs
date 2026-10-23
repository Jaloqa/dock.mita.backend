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

        var member = await db.MemberOfGroupAsync(context.GroupId, command.MemberId, cancellationToken);
        if (member is null)
        {
            return Error.NotFound();
        }

        var errors = new Dictionary<string, string[]>();
        if (member.IsLinked)
        {
            errors["memberId"] = [GroupLinkRules.MemberLinked];
        }

        var ownerEmail = await db.CurrentAsync(context.OwnerId, cancellationToken);
        if (string.Equals(ownerEmail, email, StringComparison.Ordinal))
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
            ? GroupInviteEmails.Normalize(text)
            : throw new InvalidOperationException("The email reached the handler without passing validation.");
}
