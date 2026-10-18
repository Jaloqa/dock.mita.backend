using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common.Money;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Accounts.UpdateAccount;

public sealed class UpdateAccountCurrencyValidator : AbstractValidator<UpdateAccountCommand>
{
    public UpdateAccountCurrencyValidator(AppDbContext db, ICurrentUser currentUser)
    {
        RuleFor(command => command.Currency)
            .MustAsync(async (command, _, cancellationToken) =>
            {
                if (!CurrencyWire.TryParse(command.Currency, out var requested))
                {
                    return true;
                }

                var userId = currentUser.Id;
                var stored = await db.Accounts.AsNoTracking()
                    .Where(account => account.Id == command.Id && account.UserId == userId)
                    .Select(account => new
                    {
                        account.Currency,
                        InUse = db.AccountMovements.Any(movement => movement.AccountId == account.Id)
                    })
                    .SingleOrDefaultAsync(cancellationToken);

                return stored is null || !stored.InUse || stored.Currency == requested;
            })
            .WithErrorCode("validation.currencyLocked")
            .When(command => CurrencyWire.TryParse(command.Currency, out _));
    }
}
