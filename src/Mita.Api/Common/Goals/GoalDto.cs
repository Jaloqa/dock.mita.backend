namespace Mita.Api.Common.Goals;

public sealed record GoalDto(
    Guid Id,
    string Name,
    long Target,
    long Saved,
    long Remaining,
    int Percent,
    long? Monthly,
    string Status,
    string? Deadline,
    Guid? AccountId,
    string Currency);
