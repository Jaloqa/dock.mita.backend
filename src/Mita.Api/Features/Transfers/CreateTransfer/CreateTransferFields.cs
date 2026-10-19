namespace Mita.Api.Features.Transfers.CreateTransfer;

public static class CreateTransferFields
{
    public const string FromAccountId = "fromAccountId";
    public const string ToAccountId = "toAccountId";

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
