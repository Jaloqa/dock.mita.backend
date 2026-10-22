using MediatR;
using Mita.Api.Common;

namespace Mita.Api.Features.Debts.ListRepayments;

public sealed record ListRepaymentsQuery(Guid DebtId, int Page, int PageSize)
    : IRequest<Result<PageResponse<RepaymentResponse>>>;
