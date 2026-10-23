namespace Mita.Api.Features.Debts.ListGroupDebts;

public sealed record GroupDebtDto(Guid GroupId, string Name, string Currency, long Balance);
