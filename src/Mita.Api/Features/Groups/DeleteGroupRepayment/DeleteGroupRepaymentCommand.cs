using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.DeleteGroupRepayment;

public sealed record DeleteGroupRepaymentCommand(GroupScope Scope, Guid RepaymentId) : IRequest<Result>;
