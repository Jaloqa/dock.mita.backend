using MediatR;
using Mita.Api.Common;

namespace Mita.Api.Features.Groups.LeaveGroup;

public sealed record LeaveGroupCommand(Guid GroupId) : IRequest<Result>;
