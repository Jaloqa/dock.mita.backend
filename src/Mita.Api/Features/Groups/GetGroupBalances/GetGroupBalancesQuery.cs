using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.GetGroupBalances;

public sealed record GetGroupBalancesQuery(GroupScope Scope, string? Currency)
    : IRequest<Result<GroupBalancesResponse>>;
