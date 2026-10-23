using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.GetGroup;

public sealed record GetGroupQuery(GroupScope Scope) : IRequest<Result<GroupDto>>;
