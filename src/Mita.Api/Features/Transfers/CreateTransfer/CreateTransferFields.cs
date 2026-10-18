using System.Text.Json;
using Mita.Api.Common.Wire;
using Mita.Domain.Accounts;

namespace Mita.Api.Features.Transfers.CreateTransfer;

public static class CreateTransferFields
{
    public const decimal MaxRate = 1_000_000m;

    public const string FromAccountId = "fromAccountId";
    public const string ToAccountId = "toAccountId";

    public static bool TryGetDate(JsonElement? element, out DateOnly date)
    {
        date = default;
        return JsonValues.TryGetString(element, out var text) && IsoDate.TryParse(text, out date);
    }

    public static bool TryGetAccountId(JsonElement? element, out Guid id)
    {
        id = Guid.Empty;
        return JsonValues.TryGetString(element, out var text) && Guid.TryParse(text, out id);
    }

    public static bool TryGetRate(JsonElement? element, out decimal rate)
    {
        rate = 0;
        return element is { ValueKind: JsonValueKind.Number } number
            && number.TryGetDecimal(out rate)
            && rate > 0
            && rate <= MaxRate;
    }

    public static bool TryGetNote(JsonElement? element, out string? note)
    {
        note = null;
        if (JsonValues.IsMissing(element))
        {
            return true;
        }

        if (!JsonValues.TryGetString(element, out var text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.Length > Transfer.NoteMaxLength || trimmed.Any(char.IsControl))
        {
            return false;
        }

        note = trimmed;
        return true;
    }

    public static Dictionary<string, string> AccountErrors(
        Guid? fromId,
        Guid? toId,
        IReadOnlyDictionary<Guid, bool> archivedById)
    {
        var errors = new Dictionary<string, string>();
        AddSideError(errors, FromAccountId, fromId, archivedById);
        AddSideError(errors, ToAccountId, toId, archivedById);

        if (fromId is { } from && from == toId && !errors.ContainsKey(ToAccountId))
        {
            errors[ToAccountId] = "validation.sameAccount";
        }

        return errors;
    }

    private static void AddSideError(
        Dictionary<string, string> errors,
        string field,
        Guid? id,
        IReadOnlyDictionary<Guid, bool> archivedById)
    {
        if (id is not { } accountId)
        {
            return;
        }

        if (!archivedById.TryGetValue(accountId, out var archived))
        {
            errors[field] = "validation.unknownAccount";
        }
        else if (archived)
        {
            errors[field] = "validation.archivedAccount";
        }
    }
}
