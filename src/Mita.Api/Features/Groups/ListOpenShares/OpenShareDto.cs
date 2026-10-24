namespace Mita.Api.Features.Groups.ListOpenShares;

public sealed record OpenShareDto(
    Guid ExpenseId,
    Guid PayerId,
    string Date,
    string? Description,
    string CategoryId,
    long Share,
    long Open);
