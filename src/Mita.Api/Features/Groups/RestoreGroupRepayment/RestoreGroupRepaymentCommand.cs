using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.RestoreGroupRepayment;

public sealed record RestoreGroupRepaymentCommand(GroupScope Scope, Guid RepaymentId)
    : IRequest<Result<GroupRepaymentDto>>;
