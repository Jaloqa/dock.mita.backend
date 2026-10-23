using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Groups.GetGroup;

public sealed class GetGroupHandler(AppDbContext db, GroupAccess access)
    : IRequestHandler<GetGroupQuery, Result<GroupDto>>
{
    public async Task<Result<GroupDto>> Handle(GetGroupQuery query, CancellationToken cancellationToken)
    {
        var context = await access.OpenAsync(query.Scope, GroupLock.None, cancellationToken);
        if (context is null)
        {
            return Error.NotFound();
        }

        return await db.LoadAsync(context, cancellationToken);
    }
}
