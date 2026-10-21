using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Tags.DeleteTag;

public sealed class DeleteTagHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<DeleteTagCommand, Result>
{
    public async Task<Result> Handle(DeleteTagCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;

        var deleted = await db.Tags
            .Where(tag => tag.Id == command.Id && tag.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted == 0 ? Error.NotFound() : Result.Success();
    }
}
