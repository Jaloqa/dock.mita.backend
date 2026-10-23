using System.Text.Json;
using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.UpdateGroupRepayment;

public sealed record UpdateGroupRepaymentRequest(
    JsonElement? FromMemberId,
    JsonElement? ToMemberId,
    JsonElement? Amount,
    JsonElement? Currency,
    JsonElement? Date,
    JsonElement? Rate,
    JsonElement? Note);

public sealed record UpdateGroupRepaymentCommand(
    GroupScope Scope,
    Guid RepaymentId,
    JsonElement? FromMemberId,
    JsonElement? ToMemberId,
    JsonElement? Amount,
    JsonElement? Currency,
    JsonElement? Date,
    JsonElement? Rate,
    JsonElement? Note) : IRequest<Result<GroupRepaymentDto>>, IGroupRepaymentInput;
