using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mita.Domain.Groups;

namespace Mita.Infrastructure.Persistence.Configurations;

public sealed class GroupExpenseConfiguration : IEntityTypeConfiguration<GroupExpense>
{
    private const int StandardCategoryMaxLength = 20;

    public void Configure(EntityTypeBuilder<GroupExpense> builder)
    {
        builder.ToTable(
            "group_expenses",
            table =>
            {
                table.HasCheckConstraint("CK_group_expenses_amount_positive", "\"Amount\" > 0");
                table.HasCheckConstraint("CK_group_expenses_group_amount_positive", "\"GroupAmount\" > 0");
                table.HasCheckConstraint("CK_group_expenses_rate_positive", "\"Rate\" > 0");
                table.HasCheckConstraint(
                    "CK_group_expenses_category",
                    "(\"StandardCategory\" IS NULL) <> (\"CategoryId\" IS NULL)");
            });
        builder.HasKey(expense => expense.Id);
        builder.Property(expense => expense.Id).ValueGeneratedNever();

        builder.Property(expense => expense.Currency).HasConversion<string>().HasMaxLength(3);
        builder.Property(expense => expense.StandardCategory).HasMaxLength(StandardCategoryMaxLength);
        builder.Property(expense => expense.Tags).HasColumnType("text[]");
        builder.Property(expense => expense.Description).HasMaxLength(GroupExpense.DescriptionMaxLength);
        builder.Property(expense => expense.Rate).HasPrecision(24, 12);
        builder.Property(expense => expense.Split).HasConversion<string>().HasMaxLength(10);

        builder.HasIndex(expense => new { expense.GroupId, expense.Date });

        builder.Navigation(expense => expense.Shares).HasField("shares");

        builder.HasOne<Group>()
            .WithMany()
            .HasForeignKey(expense => expense.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<GroupMember>()
            .WithMany()
            .HasForeignKey(expense => expense.PayerId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<GroupCategory>()
            .WithMany()
            .HasForeignKey(expense => expense.CategoryId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
