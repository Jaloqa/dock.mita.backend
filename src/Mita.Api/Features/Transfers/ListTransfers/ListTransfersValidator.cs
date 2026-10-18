using FluentValidation;
using Mita.Api.Common;

namespace Mita.Api.Features.Transfers.ListTransfers;

public sealed class ListTransfersValidator : AbstractValidator<ListTransfersQuery>
{
    public ListTransfersValidator()
    {
        this.RuleForPaging(query => query.Page, query => query.PageSize);

        RuleFor(query => query.AccountId)
            .Must(value => Guid.TryParse(value, out _)).WithErrorCode("validation.invalidValue")
            .When(query => query.AccountId is not null);
    }
}
