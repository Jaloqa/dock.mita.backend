using System.Text.Json;

namespace Mita.Api.Common.Budgets;

public interface IBudgetInput
{
    JsonElement? CategoryId { get; }

    JsonElement? Period { get; }

    JsonElement? Limit { get; }

    Guid? ExistingId { get; }
}
