using Mita.Domain.Accounts;
using Mita.Domain.Periods;

namespace Mita.Api.Common.Goals;

public static class GoalReferenceRules
{
    public const int DeadlineMaxMonths = 600;
    public const string AccountField = "accountId";
    public const string InvalidValue = "validation.invalidValue";
    public const string DeadlinePast = "validation.deadlinePast";
    public const string UnknownAccount = "validation.unknownAccount";
    public const string SavingsAccountOnly = "validation.savingsAccountOnly";
    public const string ArchivedAccount = "validation.archivedAccount";

    public static string? DeadlineError(PeriodKey deadline, PeriodKey? stored, DateOnly today)
    {
        if (deadline == stored)
        {
            return null;
        }

        var current = PeriodKey.Of(today);
        if (deadline < current)
        {
            return DeadlinePast;
        }

        return deadline > current.AddMonths(DeadlineMaxMonths) ? InvalidValue : null;
    }

    public static string? AccountError(AccountType? type, bool? archived, bool keepsExistingLink)
    {
        if (type is not { } stored || archived is not { } isArchived)
        {
            return UnknownAccount;
        }

        if (keepsExistingLink)
        {
            return null;
        }

        if (stored != AccountType.Savings)
        {
            return SavingsAccountOnly;
        }

        return isArchived ? ArchivedAccount : null;
    }

    public static Error Account(string code) =>
        Error.Validation(new Dictionary<string, string[]> { [AccountField] = [code] });
}
