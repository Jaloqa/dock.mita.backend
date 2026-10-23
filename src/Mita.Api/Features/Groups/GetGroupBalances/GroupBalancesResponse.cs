namespace Mita.Api.Features.Groups.GetGroupBalances;

public sealed record GroupMemberBalanceDto(Guid MemberId, long Paid, long Share, long Sent, long Received, long Balance);

public sealed record GroupTransferDto(Guid FromMemberId, Guid ToMemberId, long Amount);

public sealed record GroupBalancesResponse(
    string Currency,
    string GroupCurrency,
    long Total,
    IReadOnlyList<GroupMemberBalanceDto> Members,
    IReadOnlyList<GroupTransferDto> Transfers);
