using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.RestoreExpense;

public sealed record RestoreExpenseCommand(GroupScope Scope, Guid ExpenseId) : IRequest<Result<GroupExpenseDto>>;
