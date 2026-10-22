using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mita.Domain.Accounts;
using Mita.Domain.Debts;
using Mita.Infrastructure.Identity;

namespace Mita.Infrastructure.Persistence.Configurations;

public sealed class DebtRepaymentConfiguration : IEntityTypeConfiguration<DebtRepayment>
{
    public void Configure(EntityTypeBuilder<DebtRepayment> builder)
    {
        builder.ToTable(
            "debt_repayments",
            table =>
            {
                table.HasCheckConstraint("CK_debt_repayments_amount_positive", "\"Amount\" > 0");
                table.HasCheckConstraint("CK_debt_repayments_debt_amount_positive", "\"DebtAmount\" > 0");
                table.HasCheckConstraint(
                    "CK_debt_repayments_account_fields",
                    "(\"AccountId\" IS NULL) = (\"AccountAmount\" IS NULL)");
                table.HasCheckConstraint("CK_debt_repayments_account_amount_positive", "\"AccountAmount\" > 0");
            });
        builder.HasKey(repayment => repayment.Id);
        builder.Property(repayment => repayment.Id).ValueGeneratedNever();

        builder.Property(repayment => repayment.Currency).HasConversion<string>().HasMaxLength(3);
        builder.Property(repayment => repayment.Rate).HasPrecision(24, 12);
        builder.Property(repayment => repayment.Note).HasMaxLength(Debt.NoteMaxLength);

        builder.HasIndex(repayment => repayment.DebtId);
        builder.HasIndex(repayment => repayment.AccountId);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(repayment => repayment.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Debt>()
            .WithMany()
            .HasForeignKey(repayment => repayment.DebtId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(repayment => repayment.AccountId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
