using MediatR;
using Mita.Api.Common;

namespace Mita.Api.Features.Groups.CancelInvite;

public sealed record CancelInviteCommand(Guid GroupId, Guid MemberId) : IRequest<Result>;
