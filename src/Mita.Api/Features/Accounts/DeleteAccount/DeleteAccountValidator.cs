using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Accounts.DeleteAccount;

public sealed class DeleteAccountValidator : AbstractValidator<DeleteAccountCommand>
{
    public DeleteAccountValidator(AppDbContext db, ICurrentUser currentUser)
    {
        RuleFor(command => command.Id)
            .MustAsync(async (id, cancellationToken) =>
            {
                var userId = currentUser.Id;
                return !await db.AccountMovements.AnyAsync(
                    movement => movement.AccountId == id && movement.UserId == userId,
                    cancellationToken);
            })
            .WithErrorCode("validation.accountInUse");
    }
}
