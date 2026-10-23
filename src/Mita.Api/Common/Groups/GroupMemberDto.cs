namespace Mita.Api.Common.Groups;

public sealed record GroupMemberDto(
    Guid Id,
    string Name,
    bool InUse,
    bool Linked,
    string? LinkedName,
    string? InvitedEmail);
