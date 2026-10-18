using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Infrastructure.Persistence;
using Npgsql;

namespace Mita.Api.Features.Accounts.DeleteAccount;

public sealed class DeleteAccountHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<DeleteAccountCommand, Result>
{
    public async Task<Result> Handle(DeleteAccountCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var account = await db.Accounts.SingleOrDefaultAsync(
            candidate => candidate.Id == command.Id && candidate.UserId == userId,
            cancellationToken);
        if (account is null)
        {
            return Error.NotFound();
        }

        db.Accounts.Remove(account);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.ForeignKeyViolation
        })
        {
            return Error.Validation(new Dictionary<string, string[]> { ["id"] = ["validation.accountInUse"] });
        }

        return Result.Success();
    }
}
