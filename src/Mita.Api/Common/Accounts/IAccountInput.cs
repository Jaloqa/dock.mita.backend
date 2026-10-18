using System.Text.Json;

namespace Mita.Api.Common.Accounts;

public interface IAccountInput
{
    JsonElement? Name { get; }

    JsonElement? Type { get; }

    JsonElement? Currency { get; }

    JsonElement? OpeningBalance { get; }

    JsonElement? CardLast4 { get; }

    JsonElement? Archived { get; }
}
