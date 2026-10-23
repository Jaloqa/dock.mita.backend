using MediatR;
using Mita.Api.Common;

namespace Mita.Api.Features.Groups.DeleteGroup;

public sealed record DeleteGroupCommand(Guid Id) : IRequest<Result>;
