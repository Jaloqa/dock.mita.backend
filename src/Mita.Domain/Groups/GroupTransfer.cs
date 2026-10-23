namespace Mita.Domain.Groups;

public sealed record GroupTransfer(Guid FromMemberId, Guid ToMemberId, long Amount);
