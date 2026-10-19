using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Mita.Api.Common.Budgets;

public static class BudgetErrors
{
    private const string UniqueIndex = "IX_budgets_user_category_period";

    public static Error Category(string code) =>
        Error.Validation(new Dictionary<string, string[]> { [BudgetReferenceRules.CategoryField] = [code] });

    public static Error? FromConflict(DbUpdateException exception) => exception.InnerException switch
    {
        PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: UniqueIndex } =>
            Category(BudgetReferenceRules.BudgetExists),
        PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } =>
            Category(BudgetReferenceRules.UnknownCategory),
        _ => null
    };
}
