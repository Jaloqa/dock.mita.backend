using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mita.Domain.Groups;

namespace Mita.Infrastructure.Persistence.Configurations;

public sealed class GroupExpenseShareConfiguration : IEntityTypeConfiguration<GroupExpenseShare>
{
    public void Configure(EntityTypeBuilder<GroupExpenseShare> builder)
    {
        builder.ToTable(
            "group_expense_shares",
            table =>
            {
                table.HasCheckConstraint("CK_group_expense_shares_amount_positive", "\"Amount\" > 0");
                table.HasCheckConstraint("CK_group_expense_shares_group_amount_not_negative", "\"GroupAmount\" >= 0");
            });
        builder.HasKey(share => new { share.ExpenseId, share.MemberId });

        builder.HasIndex(share => share.MemberId);
        builder.HasIndex(share => share.GroupId);

        builder.HasOne<GroupExpense>()
            .WithMany(expense => expense.Shares)
            .HasForeignKey(share => share.ExpenseId)
            .OnDelete(DeleteBehavior.Cascade);

        // PostgreSQL runs the member check of a cascaded delete before the cascade from expenses reaches the
        // shares, so a group delete would fail on the NoAction member key; this cascade removes the shares
        // together with the members.
        builder.HasOne<Group>()
            .WithMany()
            .HasForeignKey(share => share.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<GroupMember>()
            .WithMany()
            .HasForeignKey(share => share.MemberId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
