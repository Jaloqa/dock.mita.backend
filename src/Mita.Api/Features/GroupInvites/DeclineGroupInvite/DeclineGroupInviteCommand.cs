using MediatR;
using Mita.Api.Common;

namespace Mita.Api.Features.GroupInvites.DeclineGroupInvite;

public sealed record DeclineGroupInviteCommand(Guid MemberId) : IRequest<Result>;
