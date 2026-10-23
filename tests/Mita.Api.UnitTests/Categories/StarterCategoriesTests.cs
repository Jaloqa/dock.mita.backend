using Mita.Domain.Categories;
using Mita.Domain.Users;
using Shouldly;

namespace Mita.Api.UnitTests.Categories;

public sealed class StarterCategoriesTests
{
    [Fact]
    public void Should_MatchMockStarterSet()
    {
        var all = StarterCategories.All;

        all.Count.ShouldBe(18);
        all.Count(starter => starter.Kind == CategoryKind.Expense).ShouldBe(10);
        all.Count(starter => starter.Kind == CategoryKind.Income).ShouldBe(8);
        all.Count(starter => starter is { Kind: CategoryKind.Expense, System: SystemCategory.Other }).ShouldBe(1);
        all.Count(starter => starter is { Kind: CategoryKind.Income, System: SystemCategory.Other }).ShouldBe(1);
        all.Where(starter => starter.System == SystemCategory.Transfer).Select(starter => starter.Kind)
            .ShouldBe([CategoryKind.Expense]);
        foreach (var starter in all)
        {
            foreach (var locale in Enum.GetValues<Locale>())
            {
                var name = starter.Names[locale];
                name.ShouldNotBeNullOrWhiteSpace();
                name.Length.ShouldBeLessThanOrEqualTo(Category.NameMaxLength);
            }
        }

        var userId = Guid.NewGuid();
        var first = StarterCategories.For(userId, Locale.Pl)[0];
        first.UserId.ShouldBe(userId);
        first.Name.ShouldBe("Spożywcze");
        first.Kind.ShouldBe(CategoryKind.Expense);
        first.Color.ShouldBe(CategoryColor.Chart2);
        first.System.ShouldBeNull();
        first.Position.ShouldBe(0);
    }

    [Fact]
    public void Should_NumberPositionsInStarterOrder()
    {
        var categories = StarterCategories.For(Guid.NewGuid(), Locale.Uk);

        categories.Select(category => category.Position).ShouldBe(Enumerable.Range(0, 18));
        categories.Select(category => category.Name)
            .ShouldBe(StarterCategories.All.Select(starter => starter.Names[Locale.Uk]));
        categories.Select(category => category.Id).Distinct().Count().ShouldBe(18);
    }

    [Fact]
    public void Should_CreateUserCategoryWithoutSystemAtPositionZero()
    {
        var category = Category.Create(Guid.NewGuid(), "Sport", CategoryKind.Expense, CategoryColor.Chart3);

        category.System.ShouldBeNull();
        category.Position.ShouldBe(0);

        category.Update("Salary bonus", CategoryKind.Income, CategoryColor.Chart1);

        category.Name.ShouldBe("Salary bonus");
        category.Kind.ShouldBe(CategoryKind.Income);
        category.Color.ShouldBe(CategoryColor.Chart1);
    }

    [Fact]
    public void Should_SetStandardKey_When_CategoryIsStarter()
    {
        var categories = StarterCategories.For(Guid.NewGuid(), Locale.Pl);

        categories.Select(category => category.StandardKey)
            .ShouldBe(StarterCategories.All.Select(starter => starter.Key));
        categories.Count(category => category.StandardKey == "other").ShouldBe(2);
        Category.Create(Guid.NewGuid(), "Food", CategoryKind.Expense, CategoryColor.Chart1).StandardKey.ShouldBeNull();
    }

    [Fact]
    public void Should_KeepStandardKey_When_StarterCategoryIsUpdated()
    {
        var category = StarterCategories.For(Guid.NewGuid(), Locale.En)[0];

        category.Update("Supermarket", CategoryKind.Expense, CategoryColor.Chart3);

        category.StandardKey.ShouldBe("groceries");
    }
}
