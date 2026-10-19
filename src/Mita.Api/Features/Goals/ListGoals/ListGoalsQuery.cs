using MediatR;
using Mita.Api.Common;

namespace Mita.Api.Features.Goals.ListGoals;

public sealed record ListGoalsQuery(int Page, int PageSize) : IRequest<Result<GoalPageResponse>>;
