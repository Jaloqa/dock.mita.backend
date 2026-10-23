using System.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Common.Money;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Debts.ListGroupDebts;

public sealed class ListGroupDebtsHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ListGroupDebtsQuery, Result<PageResponse<GroupDebtDto>>>
{
    public async Task<Result<PageResponse<GroupDebtDto>>> Handle(
        ListGroupDebtsQuery query,
        CancellationToken cancellationToken)
    {
        await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);

        var balances = await db.LinkedBalancesAsync(currentUser.Id, cancellationToken);
        var request = new PageRequest(query.Page, query.PageSize);

        return new PageResponse<GroupDebtDto>(
            [
                .. balances
                    .Skip(request.Skip)
                    .Take(request.PageSize)
                    .Select(balance => new GroupDebtDto(
                        balance.GroupId,
                        balance.Name,
                        CurrencyWire.ToWire(balance.Currency),
                        balance.Balance))
            ],
            request.Page,
            request.PageSize,
            balances.Count);
    }
}
