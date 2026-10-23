using System.Text.Json;

namespace Mita.Api.Common.Groups;

public interface IExpenseInput
{
    JsonElement? PayerId { get; }

    JsonElement? Amount { get; }

    JsonElement? Currency { get; }

    JsonElement? Date { get; }

    JsonElement? CategoryId { get; }

    JsonElement? Tags { get; }

    JsonElement? Description { get; }

    JsonElement? Rate { get; }

    JsonElement? Split { get; }
}
