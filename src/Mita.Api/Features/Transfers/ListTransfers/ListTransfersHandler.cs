using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Transfers;
using Mita.Api.Common.Wire;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Transfers.ListTransfers;

public sealed class ListTransfersHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ListTransfersQuery, Result<PageResponse<TransferDto>>>
{
    public async Task<Result<PageResponse<TransferDto>>> Handle(ListTransfersQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var transfers = db.Transfers.AsNoTracking().Where(transfer => transfer.UserId == userId);
        if (query.AccountId is not null)
        {
            var accountId = Guid.Parse(query.AccountId);
            transfers = transfers.Where(transfer => transfer.FromAccountId == accountId || transfer.ToAccountId == accountId);
        }

        return await transfers
            .OrderByDescending(transfer => transfer.Date)
            .ThenByDescending(transfer => transfer.CreatedAt)
            .ThenByDescending(transfer => transfer.Id)
            .Select(transfer => new TransferDto(
                transfer.Id,
                IsoDate.ToWire(transfer.Date),
                transfer.FromAccountId,
                transfer.ToAccountId,
                transfer.FromAmount,
                transfer.ToAmount,
                (double)transfer.Rate,
                transfer.Note))
            .ToPageAsync(new PageRequest(query.Page, query.PageSize), cancellationToken);
    }
}
