using MediatR;
using Mita.Api.Common;

namespace Mita.Api.Features.Debts.ListDebts;

public sealed record ListDebtsQuery(string? Status, int Page, int PageSize) : IRequest<Result<DebtPageResponse>>;
