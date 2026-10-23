using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.LinkThroughShare;

public sealed record LinkThroughShareCommand(GroupScope Scope) : IRequest<Result<GroupSummaryDto>>;
