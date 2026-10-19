using Mita.Domain.Categories;

namespace Mita.Api.Common.Budgets;

public static class BudgetReferenceRules
{
    public const string CategoryField = "categoryId";
    public const string UnknownCategory = "validation.unknownCategory";
    public const string ExpenseCategoryOnly = "validation.expenseCategoryOnly";
    public const string BudgetExists = "validation.budgetExists";

    public static string? CategoryError(CategoryKind? kind, SystemCategory? system) =>
        kind is not { } stored || system == SystemCategory.Transfer
            ? UnknownCategory
            : stored == CategoryKind.Income
                ? ExpenseCategoryOnly
                : null;
}
