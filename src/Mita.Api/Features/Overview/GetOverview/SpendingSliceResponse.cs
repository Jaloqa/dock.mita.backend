namespace Mita.Api.Features.Overview.GetOverview;

public sealed record SpendingSliceResponse(Guid? CategoryId, string? Name, long Amount, double Percent);
