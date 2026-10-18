namespace Mita.Api.Common.Accounts;

public sealed record AccountDto(
    Guid Id,
    string Name,
    string Type,
    string Currency,
    long OpeningBalance,
    long Balance,
    string? CardLast4,
    bool Archived,
    bool InUse,
    int LinkedGoals);
