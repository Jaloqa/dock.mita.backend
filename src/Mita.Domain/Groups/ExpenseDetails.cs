using Mita.Domain.Money;

namespace Mita.Domain.Groups;

public sealed record ExpenseDetails(
    Guid PayerId,
    long Amount,
    Currency Currency,
    DateOnly Date,
    string? StandardCategory,
    Guid? CategoryId,
    IReadOnlyList<string> Tags,
    string? Description,
    decimal Rate,
    bool RateManual,
    long GroupAmount,
    GroupSplitMode Split,
    IReadOnlyList<GroupShare> Shares);
