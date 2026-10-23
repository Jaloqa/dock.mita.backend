using System.Text.Json;
using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.UpdateExpense;

public sealed record UpdateExpenseRequest(
    JsonElement? PayerId,
    JsonElement? Amount,
    JsonElement? Currency,
    JsonElement? Date,
    JsonElement? CategoryId,
    JsonElement? Tags,
    JsonElement? Description,
    JsonElement? Rate,
    JsonElement? Split);

public sealed record UpdateExpenseCommand(
    GroupScope Scope,
    Guid ExpenseId,
    JsonElement? PayerId,
    JsonElement? Amount,
    JsonElement? Currency,
    JsonElement? Date,
    JsonElement? CategoryId,
    JsonElement? Tags,
    JsonElement? Description,
    JsonElement? Rate,
    JsonElement? Split) : IRequest<Result<GroupExpenseDto>>, IExpenseInput;
