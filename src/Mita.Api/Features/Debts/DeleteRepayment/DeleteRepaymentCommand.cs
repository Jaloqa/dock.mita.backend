using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Accounts;
using Mita.Api.Common.Caching;

namespace Mita.Api.Features.Debts.DeleteRepayment;

public sealed record DeleteRepaymentCommand(Guid DebtId, Guid RepaymentId) : IRequest<Result>, IInvalidatesCache
{
    public IReadOnlyCollection<string> CacheTagsToInvalidate => [AccountCacheTags.Accounts];
}
