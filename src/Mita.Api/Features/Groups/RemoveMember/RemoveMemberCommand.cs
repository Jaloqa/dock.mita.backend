using MediatR;
using Mita.Api.Common;

namespace Mita.Api.Features.Groups.RemoveMember;

public sealed record RemoveMemberCommand(Guid GroupId, Guid MemberId) : IRequest<Result>;
