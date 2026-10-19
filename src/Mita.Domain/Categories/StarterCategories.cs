using System.Collections.ObjectModel;
using Mita.Domain.Users;

namespace Mita.Domain.Categories;

public static class StarterCategories
{
    public static IReadOnlyList<StarterCategory> All { get; } =
    [
        Expense("groceries", CategoryColor.Chart2, null, "Groceries", "Spożywcze", "Продукты", "Продукти"),
        Expense("transport", CategoryColor.Chart5, null, "Transport", "Transport", "Транспорт", "Транспорт"),
        Expense("housing", CategoryColor.Chart1, null, "Housing", "Mieszkanie", "Жильё", "Житло"),
        Expense("cafes", CategoryColor.Chart4, null, "Cafes", "Kawiarnie", "Кафе", "Кафе"),
        Expense("health", CategoryColor.Chart3, null, "Health", "Zdrowie", "Здоровье", "Здоров'я"),
        Expense("shopping", CategoryColor.Chart4, null, "Shopping", "Zakupy", "Покупки", "Покупки"),
        Expense("leisure", CategoryColor.Chart3, null, "Leisure", "Rozrywka", "Досуг", "Дозвілля"),
        Expense("gifts", CategoryColor.Chart2, null, "Gifts", "Prezenty", "Подарки", "Подарунки"),
        Expense("other", CategoryColor.Chart6, SystemCategory.Other, "Other", "Inne", "Другое", "Інше"),
        Expense("transfer", CategoryColor.Chart5, SystemCategory.Transfer, "Transfers", "Przelewy", "Переводы", "Перекази"),
        Income("salary", CategoryColor.Chart2, null, "Salary", "Wynagrodzenie", "Зарплата", "Зарплата"),
        Income("freelance", CategoryColor.Chart1, null, "Freelance", "Freelance", "Фриланс", "Фриланс"),
        Income("gift", CategoryColor.Chart4, null, "Gift", "Prezent", "Подарок", "Подарунок"),
        Income("interest", CategoryColor.Chart3, null, "Interest", "Odsetki", "Проценты", "Відсотки"),
        Income("refund", CategoryColor.Chart5, null, "Refund", "Zwrot", "Возврат", "Повернення"),
        Income("sale", CategoryColor.Chart1, null, "Sale", "Sprzedaż", "Продажа", "Продаж"),
        Income("bonus", CategoryColor.Chart4, null, "Bonus", "Premia", "Бонус", "Бонус"),
        Income("other", CategoryColor.Chart6, SystemCategory.Other, "Other", "Inne", "Другое", "Інше")
    ];

    public static IReadOnlyList<Category> For(Guid userId, Locale locale) =>
        All.Select((starter, position) => Category.Starter(userId, starter, locale, position)).ToList();

    private static StarterCategory Expense(
        string key,
        CategoryColor color,
        SystemCategory? system,
        string en,
        string pl,
        string ru,
        string uk) =>
        new(key, CategoryKind.Expense, color, system, Names(en, pl, ru, uk));

    private static StarterCategory Income(
        string key,
        CategoryColor color,
        SystemCategory? system,
        string en,
        string pl,
        string ru,
        string uk) =>
        new(key, CategoryKind.Income, color, system, Names(en, pl, ru, uk));

    private static ReadOnlyDictionary<Locale, string> Names(string en, string pl, string ru, string uk) =>
        new Dictionary<Locale, string>
        {
            [Locale.En] = en,
            [Locale.Pl] = pl,
            [Locale.Ru] = ru,
            [Locale.Uk] = uk
        }.AsReadOnly();
}
