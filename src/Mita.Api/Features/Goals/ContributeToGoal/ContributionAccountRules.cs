namespace Mita.Api.Features.Goals.ContributeToGoal;

public static class ContributionAccountRules
{
    public const string AccountField = "accountId";
    public const string FromAccountField = "fromAccountId";
    public const string Required = "validation.required";
    public const string UnknownAccount = "validation.unknownAccount";
    public const string ArchivedAccount = "validation.archivedAccount";
    public const string SameAccount = "validation.sameAccount";

    public static Dictionary<string, string> Errors(
        Guid? linkedId,
        bool linkedArchived,
        Guid? fromId,
        bool fromPresent,
        IReadOnlyDictionary<Guid, bool> archivedById)
    {
        var errors = new Dictionary<string, string>();
        if (linkedId is not null && linkedArchived)
        {
            errors[AccountField] = ArchivedAccount;
        }

        if (linkedId is null && !fromPresent)
        {
            return errors;
        }

        if (!fromPresent)
        {
            errors[FromAccountField] = Required;
        }
        else if (fromId is not { } from || !archivedById.TryGetValue(from, out var archived))
        {
            errors[FromAccountField] = UnknownAccount;
        }
        else if (archived)
        {
            errors[FromAccountField] = ArchivedAccount;
        }
        else if (from == linkedId)
        {
            errors[FromAccountField] = SameAccount;
        }

        return errors;
    }
}
