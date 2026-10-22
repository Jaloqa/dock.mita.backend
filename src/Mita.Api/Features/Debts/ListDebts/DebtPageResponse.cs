using Mita.Api.Common.Debts;

namespace Mita.Api.Features.Debts.ListDebts;

public sealed record DebtPageResponse(
    IReadOnlyList<DebtDto> Items,
    int Page,
    int PageSize,
    int Total,
    string Currency,
    DebtSummaryDto Summary);
