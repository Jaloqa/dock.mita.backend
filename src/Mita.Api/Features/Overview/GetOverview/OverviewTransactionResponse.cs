namespace Mita.Api.Features.Overview.GetOverview;

public sealed record OverviewTransactionResponse(
    Guid Id,
    string Kind,
    string Date,
    string Title,
    string CategoryName,
    string AccountName,
    long Amount);
