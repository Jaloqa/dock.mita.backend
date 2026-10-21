using System.Text.Json;

namespace Mita.Api.Common.Transactions;

public interface ITransactionInput
{
    JsonElement? Kind { get; }

    JsonElement? Date { get; }

    JsonElement? Title { get; }

    JsonElement? Note { get; }

    JsonElement? CategoryId { get; }

    JsonElement? AccountId { get; }

    JsonElement? Amount { get; }

    JsonElement? Currency { get; }

    JsonElement? Rate { get; }

    JsonElement? Tags { get; }

    Guid? ExistingId { get; }
}
