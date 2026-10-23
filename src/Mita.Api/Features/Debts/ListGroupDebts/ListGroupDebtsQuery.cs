using MediatR;
using Mita.Api.Common;

namespace Mita.Api.Features.Debts.ListGroupDebts;

public sealed record ListGroupDebtsQuery(int Page, int PageSize) : IRequest<Result<PageResponse<GroupDebtDto>>>;
