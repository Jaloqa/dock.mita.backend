namespace Mita.Api.Common.Groups;

public sealed record GroupDto(
    Guid Id,
    string Name,
    string Currency,
    bool CurrencyLocked,
    long Total,
    IReadOnlyList<GroupMemberDto> Members,
    IReadOnlyList<GroupCategoryDto> Categories,
    string? ShareToken,
    string Role,
    Guid? MyMemberId);
