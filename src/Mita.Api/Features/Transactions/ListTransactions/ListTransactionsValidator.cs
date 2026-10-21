using FluentValidation;
using Mita.Api.Common;
using Mita.Api.Common.Categories;
using Mita.Api.Common.Wire;
using Mita.Domain.Categories;

namespace Mita.Api.Features.Transactions.ListTransactions;

public sealed class ListTransactionsValidator : AbstractValidator<ListTransactionsQuery>
{
    private const string InvalidValue = "validation.invalidValue";

    public ListTransactionsValidator()
    {
        this.RuleForPaging(query => query.Page, query => query.PageSize);

        RuleFor(query => query.Kind)
            .Must(kind => CategoryWire.TryParse(kind, out CategoryKind _))
            .When(query => query.Kind is not null)
            .WithErrorCode(InvalidValue);

        RuleFor(query => query.From)
            .Must(from => IsoDate.TryParse(from, out _))
            .When(query => query.From is not null)
            .WithErrorCode(InvalidValue);

        RuleFor(query => query.To)
            .Must(to => IsoDate.TryParse(to, out _))
            .When(query => query.To is not null)
            .WithErrorCode(InvalidValue);

        RuleFor(query => query.Sort)
            .Must(sort => TransactionSort.TryParse(sort, out _))
            .When(query => query.Sort is not null)
            .WithErrorCode(InvalidValue);

        RuleFor(query => query.CategoryId)
            .Must(id => Guid.TryParse(id, out _))
            .When(query => query.CategoryId is not null)
            .WithErrorCode(InvalidValue);

        RuleFor(query => query.AccountId)
            .Must(id => Guid.TryParse(id, out _))
            .When(query => query.AccountId is not null)
            .WithErrorCode(InvalidValue);

        RuleFor(query => query.TagIds)
            .Must(ids => TagIdList.TryParse(ids, out _))
            .WithErrorCode(InvalidValue);
    }
}
