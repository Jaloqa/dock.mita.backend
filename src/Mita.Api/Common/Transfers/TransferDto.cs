namespace Mita.Api.Common.Transfers;

public sealed record TransferDto(
    Guid Id,
    string Date,
    Guid FromAccountId,
    Guid ToAccountId,
    long FromAmount,
    long ToAmount,
    double Rate,
    string? Note);
