namespace Mita.Domain.Groups;

public sealed record GroupShare(Guid MemberId, long Amount, long GroupAmount);
