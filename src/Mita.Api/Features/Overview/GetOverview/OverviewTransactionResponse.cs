namespace Mita.Api.Features.Overview.GetOverview;

public sealed record OverviewTransactionResponse(
    Guid Id,
    string Kind,
    string Date,
    string Title,
    Guid CategoryId,
    string CategoryName,
    string CategoryColor,
    string AccountName,
    long Amount);
