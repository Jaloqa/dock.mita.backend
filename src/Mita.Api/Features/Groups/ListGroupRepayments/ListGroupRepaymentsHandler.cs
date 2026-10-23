using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Groups.ListGroupRepayments;

public sealed class ListGroupRepaymentsHandler(AppDbContext db, GroupAccess access)
    : IRequestHandler<ListGroupRepaymentsQuery, Result<PageResponse<GroupRepaymentDto>>>
{
    public async Task<Result<PageResponse<GroupRepaymentDto>>> Handle(
        ListGroupRepaymentsQuery query,
        CancellationToken cancellationToken)
    {
        if (!GroupWire.TryParseDeletedFlag(query.Deleted, out var deleted))
        {
            throw new InvalidOperationException("The deleted flag must be validated before the handler runs.");
        }

        var group = await access.OpenAsync(query.Scope, GroupLock.None, cancellationToken);
        if (group is null)
        {
            return Error.NotFound();
        }

        return await GroupRepaymentQueries.PageAsync(
            db,
            group,
            deleted,
            new PageRequest(query.Page, query.PageSize),
            cancellationToken);
    }
}
