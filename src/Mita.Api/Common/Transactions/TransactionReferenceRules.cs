using Mita.Domain.Categories;

namespace Mita.Api.Common.Transactions;

public static class TransactionReferenceRules
{
    public const string CategoryField = "categoryId";
    public const string AccountField = "accountId";
    public const string UnknownCategory = "validation.unknownCategory";
    public const string CategoryKindMismatch = "validation.categoryKindMismatch";
    public const string UnknownAccount = "validation.unknownAccount";
    public const string ArchivedAccount = "validation.archivedAccount";

    public static string? CategoryError(CategoryKind? storedKind, SystemCategory? system, CategoryKind? requestedKind) =>
        storedKind is not { } kind || system == SystemCategory.Transfer
            ? UnknownCategory
            : requestedKind is { } requested && requested != kind
                ? CategoryKindMismatch
                : null;

    public static string? AccountError(bool? archived, bool keepsExistingAccount) => archived switch
    {
        null => UnknownAccount,
        true when !keepsExistingAccount => ArchivedAccount,
        _ => null
    };
}
