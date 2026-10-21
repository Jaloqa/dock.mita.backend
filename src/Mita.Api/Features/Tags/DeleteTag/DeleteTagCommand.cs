using MediatR;
using Mita.Api.Common;

namespace Mita.Api.Features.Tags.DeleteTag;

public sealed record DeleteTagCommand(Guid Id) : IRequest<Result>;
