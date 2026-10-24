using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.ListOpenShares;

public sealed record ListOpenSharesQuery(GroupScope Scope) : IRequest<Result<IReadOnlyList<OpenShareDto>>>;
