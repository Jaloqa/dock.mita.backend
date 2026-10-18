using MediatR;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Accounts;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Accounts.ListAccounts;

public sealed class ListAccountsHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ListAccountsQuery, Result<PageResponse<AccountDto>>>
{
    public async Task<Result<PageResponse<AccountDto>>> Handle(ListAccountsQuery query, CancellationToken cancellationToken) =>
        await db.ProjectAccounts(currentUser.Id)
            .ToPageAsync(new PageRequest(query.Page, query.PageSize), cancellationToken);
}
