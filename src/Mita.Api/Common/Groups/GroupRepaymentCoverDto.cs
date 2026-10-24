namespace Mita.Api.Common.Groups;

public sealed record GroupRepaymentCoverDto(Guid ExpenseId, long GroupAmount, string? Description, string CategoryId);
