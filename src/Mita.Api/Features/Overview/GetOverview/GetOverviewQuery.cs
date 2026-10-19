using MediatR;
using Mita.Api.Common;

namespace Mita.Api.Features.Overview.GetOverview;

public sealed record GetOverviewQuery(string? Period) : IRequest<Result<OverviewResponse>>;
