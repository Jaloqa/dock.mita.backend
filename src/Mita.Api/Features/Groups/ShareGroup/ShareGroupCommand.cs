using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.ShareGroup;

public sealed record ShareGroupCommand(Guid Id) : IRequest<Result<GroupDto>>;
