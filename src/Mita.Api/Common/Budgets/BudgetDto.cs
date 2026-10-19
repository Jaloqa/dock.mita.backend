namespace Mita.Api.Common.Budgets;

public sealed record BudgetDto(
    Guid Id,
    Guid CategoryId,
    string Period,
    long Limit,
    long Spent,
    long Remaining,
    string Status,
    long? PerDay);
