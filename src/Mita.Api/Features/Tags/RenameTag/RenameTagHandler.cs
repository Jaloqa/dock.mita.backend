using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Tags;
using Mita.Infrastructure.Persistence;
using Npgsql;

namespace Mita.Api.Features.Tags.RenameTag;

public sealed class RenameTagHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<RenameTagCommand, Result<TagDto>>
{
    private const string NameIndex = "IX_tags_user_name";

    public async Task<Result<TagDto>> Handle(RenameTagCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        if (!TagNames.TryParseName(command.Name, out var name))
        {
            throw new InvalidOperationException("The tag name reached the handler without passing validation.");
        }

        var tag = await db.Tags.SingleOrDefaultAsync(
            candidate => candidate.Id == command.Id && candidate.UserId == userId,
            cancellationToken);
        if (tag is null)
        {
            return Error.NotFound();
        }

        tag.Rename(name);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: NameIndex
        })
        {
            return Error.Validation(new Dictionary<string, string[]> { ["name"] = ["validation.tagNameTaken"] });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Error.NotFound();
        }

        var projected = await db.ProjectTag(userId, tag.Id).SingleOrDefaultAsync(cancellationToken);
        if (projected is null)
        {
            return Error.NotFound();
        }

        return projected;
    }
}
