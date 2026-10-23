using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mita.Domain.Groups;

namespace Mita.Infrastructure.Persistence.Configurations;

public sealed class GroupRepaymentConfiguration : IEntityTypeConfiguration<GroupRepayment>
{
    public void Configure(EntityTypeBuilder<GroupRepayment> builder)
    {
        builder.ToTable(
            "group_repayments",
            table =>
            {
                table.HasCheckConstraint("CK_group_repayments_amount_positive", "\"Amount\" > 0");
                table.HasCheckConstraint("CK_group_repayments_group_amount_positive", "\"GroupAmount\" > 0");
                table.HasCheckConstraint("CK_group_repayments_rate_positive", "\"Rate\" > 0");
                table.HasCheckConstraint("CK_group_repayments_members_differ", "\"FromMemberId\" <> \"ToMemberId\"");
            });
        builder.HasKey(repayment => repayment.Id);
        builder.Property(repayment => repayment.Id).ValueGeneratedNever();

        builder.Property(repayment => repayment.Currency).HasConversion<string>().HasMaxLength(3);
        builder.Property(repayment => repayment.Rate).HasPrecision(24, 12);
        builder.Property(repayment => repayment.Note).HasMaxLength(GroupRepayment.NoteMaxLength);

        builder.HasIndex(repayment => new { repayment.GroupId, repayment.Date });
        builder.HasIndex(repayment => repayment.FromMemberId);
        builder.HasIndex(repayment => repayment.ToMemberId);

        builder.HasOne<Group>()
            .WithMany()
            .HasForeignKey(repayment => repayment.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<GroupMember>()
            .WithMany()
            .HasForeignKey(repayment => repayment.FromMemberId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<GroupMember>()
            .WithMany()
            .HasForeignKey(repayment => repayment.ToMemberId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
