using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Common.Wire;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Groups.ListOpenShares;

public sealed class ListOpenSharesHandler(AppDbContext db, GroupAccess access)
    : IRequestHandler<ListOpenSharesQuery, Result<IReadOnlyList<OpenShareDto>>>
{
    public async Task<Result<IReadOnlyList<OpenShareDto>>> Handle(
        ListOpenSharesQuery query,
        CancellationToken cancellationToken)
    {
        var group = await access.OpenAsync(query.Scope, GroupLock.None, cancellationToken);
        if (group is null)
        {
            return Error.NotFound();
        }

        if (group.MyMemberId is not { } memberId)
        {
            return new List<OpenShareDto>();
        }

        var shares = await db.OpenSharesAsync(group.GroupId, memberId, null, null, cancellationToken);
        return shares
            .Select(share => new OpenShareDto(
                share.ExpenseId,
                share.PayerId,
                IsoDate.ToWire(share.Date),
                share.Description,
                share.CategoryId,
                share.Share,
                share.Open))
            .ToList();
    }
}
