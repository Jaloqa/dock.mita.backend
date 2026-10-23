using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.DeleteExpense;

public sealed record DeleteExpenseCommand(GroupScope Scope, Guid ExpenseId) : IRequest<Result>;
