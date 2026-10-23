using MediatR;
using Mita.Api.Common;

namespace Mita.Api.Features.Groups.UnshareGroup;

public sealed record UnshareGroupCommand(Guid Id) : IRequest<Result>;
