using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mita.Domain.Groups;

namespace Mita.Infrastructure.Persistence.Configurations;

public sealed class GroupRepaymentCoverConfiguration : IEntityTypeConfiguration<GroupRepaymentCover>
{
    public void Configure(EntityTypeBuilder<GroupRepaymentCover> builder)
    {
        builder.ToTable(
            "group_repayment_covers",
            table => table.HasCheckConstraint("CK_group_repayment_covers_amount_positive", "\"GroupAmount\" > 0"));
        builder.HasKey(cover => new { cover.RepaymentId, cover.ExpenseId });

        builder.HasIndex(cover => cover.ExpenseId);
        builder.HasIndex(cover => cover.GroupId);

        builder.HasOne<GroupRepayment>()
            .WithMany(repayment => repayment.Covers)
            .HasForeignKey(cover => cover.RepaymentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<GroupExpense>()
            .WithMany()
            .HasForeignKey(cover => cover.ExpenseId)
            .OnDelete(DeleteBehavior.NoAction);

        // Same reason as the shares: a group delete checks the NoAction expense key before the cascade from
        // repayments reaches the covers, so the covers go together with the group.
        builder.HasOne<Group>()
            .WithMany()
            .HasForeignKey(cover => cover.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
