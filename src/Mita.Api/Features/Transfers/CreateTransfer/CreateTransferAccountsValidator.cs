using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common.Wire;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Transfers.CreateTransfer;

public sealed class CreateTransferAccountsValidator : AbstractValidator<CreateTransferCommand>
{
    public CreateTransferAccountsValidator(AppDbContext db, ICurrentUser currentUser)
    {
        RuleFor(command => command).CustomAsync(async (command, context, cancellationToken) =>
        {
            Guid? fromId = JsonValues.TryGetGuid(command.FromAccountId, out var from) ? from : null;
            Guid? toId = JsonValues.TryGetGuid(command.ToAccountId, out var to) ? to : null;
            Guid[] ids = [.. new[] { fromId, toId }.OfType<Guid>().Distinct()];
            if (ids.Length == 0)
            {
                return;
            }

            var userId = currentUser.Id;
            var archivedById = await db.Accounts.AsNoTracking()
                .Where(account => account.UserId == userId && ids.Contains(account.Id))
                .ToDictionaryAsync(account => account.Id, account => account.Archived, cancellationToken);

            foreach (var (field, code) in CreateTransferFields.AccountErrors(fromId, toId, archivedById))
            {
                context.AddFailure(new ValidationFailure(field, code) { ErrorCode = code });
            }
        });
    }
}
