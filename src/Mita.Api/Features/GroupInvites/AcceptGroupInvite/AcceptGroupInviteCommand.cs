using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.GroupInvites.AcceptGroupInvite;

public sealed record AcceptGroupInviteCommand(Guid MemberId) : IRequest<Result<GroupSummaryDto>>;
