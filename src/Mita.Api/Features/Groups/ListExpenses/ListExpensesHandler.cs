using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Groups.ListExpenses;

public sealed class ListExpensesHandler(AppDbContext db, GroupAccess access)
    : IRequestHandler<ListExpensesQuery, Result<PageResponse<GroupExpenseDto>>>
{
    public async Task<Result<PageResponse<GroupExpenseDto>>> Handle(
        ListExpensesQuery query,
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

        return await db.PageAsync(group, deleted, new PageRequest(query.Page, query.PageSize), cancellationToken);
    }
}
