using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Accounts;
using Mita.Domain.Accounts;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Accounts.CreateAccount;

public sealed class CreateAccountHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<CreateAccountCommand, Result<AccountDto>>
{
    public async Task<Result<AccountDto>> Handle(CreateAccountCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var input = AccountInputParser.Parse(command);

        var account = Account.Create(
            userId,
            input.Name,
            input.Type,
            input.Currency,
            input.OpeningBalance,
            input.CardLast4,
            input.Archived ?? false);

        db.Accounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);

        return await db.ProjectAccount(userId, account.Id).SingleAsync(cancellationToken);
    }
}
