using System.Text.Json;

namespace Mita.Api.Common.Debts;

public interface IDebtInput
{
    JsonElement? Direction { get; }

    JsonElement? Counterparty { get; }

    JsonElement? Amount { get; }

    JsonElement? Currency { get; }

    JsonElement? Date { get; }

    JsonElement? DueDate { get; }

    JsonElement? AccountId { get; }

    JsonElement? Rate { get; }

    JsonElement? Note { get; }

    Guid? ExistingId { get; }
}
