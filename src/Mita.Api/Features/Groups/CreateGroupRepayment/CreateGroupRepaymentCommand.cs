using System.Text.Json;
using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.CreateGroupRepayment;

public sealed record CreateGroupRepaymentRequest(
    JsonElement? FromMemberId,
    JsonElement? ToMemberId,
    JsonElement? Amount,
    JsonElement? Currency,
    JsonElement? Date,
    JsonElement? Rate,
    JsonElement? Note,
    JsonElement? Settles);

public sealed record CreateGroupRepaymentCommand(
    GroupScope Scope,
    JsonElement? FromMemberId,
    JsonElement? ToMemberId,
    JsonElement? Amount,
    JsonElement? Currency,
    JsonElement? Date,
    JsonElement? Rate,
    JsonElement? Note,
    JsonElement? Settles) : IRequest<Result<GroupRepaymentDto>>, IGroupRepaymentInput;
