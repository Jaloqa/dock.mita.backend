using System.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Common.Money;
using Mita.Api.Common.Rates;
using Mita.Api.Common.Time;
using Mita.Domain.Groups;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Groups.GetGroupBalances;

public sealed class GetGroupBalancesHandler(AppDbContext db, GroupAccess access, TimeProvider time)
    : IRequestHandler<GetGroupBalancesQuery, Result<GroupBalancesResponse>>
{
    public async Task<Result<GroupBalancesResponse>> Handle(
        GetGroupBalancesQuery query,
        CancellationToken cancellationToken)
    {
        await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);

        var context = await access.OpenAsync(query.Scope, GroupLock.None, cancellationToken);
        if (context is null)
        {
            return Error.NotFound();
        }

        var view = context.Currency;
        if (query.Currency is not null && !CurrencyWire.TryParse(query.Currency, out view))
        {
            throw new InvalidOperationException("The view currency must be validated before the handler runs.");
        }

        var rate = 1m;
        if (view != context.Currency)
        {
            var plnValues = await db.PlnValuesAsync(UtcCalendar.Today(time), cancellationToken);
            if (RateTable.Rate(plnValues, context.Currency, view) is not { } found)
            {
                return Error.Unavailable("rates.unavailable");
            }

            rate = found;
        }

        var memberIds = await db.GroupMembers.AsNoTracking()
            .Where(member => member.GroupId == context.GroupId)
            .OrderBy(member => member.Position)
            .Select(member => member.Id)
            .ToListAsync(cancellationToken);

        var figures = await db.FiguresAsync(memberIds, cancellationToken);
        var members = memberIds.Select(memberId => figures[memberId]).ToList();

        var transfers = GroupSettlement.Suggest([.. members.Select(member => (member.MemberId, member.Balance))]);

        long InView(long amount) => MinorUnits.Convert(amount, rate, context.Currency, view);

        return new GroupBalancesResponse(
            CurrencyWire.ToWire(view),
            CurrencyWire.ToWire(context.Currency),
            InView(members.Sum(member => member.Paid)),
            [
                .. members.Select(member => new GroupMemberBalanceDto(
                    member.MemberId,
                    InView(member.Paid),
                    InView(member.Share),
                    InView(member.Sent),
                    InView(member.Received),
                    InView(member.Balance)))
            ],
            [
                .. transfers.Select(transfer => new GroupTransferDto(
                    transfer.FromMemberId,
                    transfer.ToMemberId,
                    InView(transfer.Amount)))
            ]);
    }
}
