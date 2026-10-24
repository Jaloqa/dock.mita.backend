using System.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.ToConfirm.ListToConfirm;

public sealed class ListToConfirmHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ListToConfirmQuery, Result<PageResponse<ToConfirmItemDto>>>
{
    public async Task<Result<PageResponse<ToConfirmItemDto>>> Handle(
        ListToConfirmQuery query,
        CancellationToken cancellationToken)
    {
        var page = new PageRequest(query.Page, query.PageSize);

        // Terms and versions come from one snapshot: read in separate statements, an edit committed in between could
        // pair the old terms with the new version, and a confirmation sent with that version would book unseen terms.
        await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var items = await db.PendingAsync(currentUser.Id, cancellationToken);

        return new PageResponse<ToConfirmItemDto>(
            [.. items.Skip(page.Skip).Take(page.PageSize).Select(ToConfirmItemDto.From)],
            page.Page,
            page.PageSize,
            items.Count);
    }
}
