using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mita.Domain.Budgets;
using Mita.Domain.Categories;
using Mita.Infrastructure.Identity;

namespace Mita.Infrastructure.Persistence.Configurations;

public sealed class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.ToTable("budgets", table => table.HasCheckConstraint("CK_budgets_limit_positive", "\"Limit\" > 0"));
        builder.HasKey(budget => budget.Id);
        builder.Property(budget => budget.Id).ValueGeneratedNever();

        builder.Property(budget => budget.Period).HasConversion(PeriodKeyConverters.Required).HasMaxLength(7);
        builder.Property(budget => budget.Currency).HasConversion<string>().HasMaxLength(3);

        builder.HasIndex(budget => new { budget.UserId, budget.CategoryId, budget.Period })
            .IsUnique()
            .HasDatabaseName("IX_budgets_user_category_period");
        builder.HasIndex(budget => new { budget.UserId, budget.Period });

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(budget => budget.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(budget => budget.CategoryId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
