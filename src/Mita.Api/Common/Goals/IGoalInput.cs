using System.Text.Json;

namespace Mita.Api.Common.Goals;

public interface IGoalInput
{
    JsonElement? Name { get; }

    JsonElement? Target { get; }

    JsonElement? Deadline { get; }

    JsonElement? AccountId { get; }

    Guid? ExistingId { get; }
}
