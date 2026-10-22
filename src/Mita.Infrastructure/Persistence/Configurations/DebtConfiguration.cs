using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mita.Domain.Accounts;
using Mita.Domain.Debts;
using Mita.Infrastructure.Identity;

namespace Mita.Infrastructure.Persistence.Configurations;

public sealed class DebtConfiguration : IEntityTypeConfiguration<Debt>
{
    public void Configure(EntityTypeBuilder<Debt> builder)
    {
        builder.ToTable(
            "debts",
            table =>
            {
                table.HasCheckConstraint("CK_debts_amount_positive", "\"Amount\" > 0");
                table.HasCheckConstraint("CK_debts_repaid_range", "\"Repaid\" >= 0 AND \"Repaid\" <= \"Amount\"");
                table.HasCheckConstraint(
                    "CK_debts_account_fields",
                    "(\"AccountId\" IS NULL) = (\"AccountAmount\" IS NULL) AND (\"AccountId\" IS NULL) = (\"Rate\" IS NULL)");
                table.HasCheckConstraint("CK_debts_account_amount_positive", "\"AccountAmount\" > 0");
                table.HasCheckConstraint("CK_debts_due_after_date", "\"DueDate\" >= \"Date\"");
            });
        builder.HasKey(debt => debt.Id);
        builder.Property(debt => debt.Id).ValueGeneratedNever();

        builder.Property(debt => debt.Direction).HasConversion<string>().HasMaxLength(10);
        builder.Property(debt => debt.Counterparty).HasMaxLength(Debt.CounterpartyMaxLength).IsRequired();
        builder.Property(debt => debt.Currency).HasConversion<string>().HasMaxLength(3);
        builder.Property(debt => debt.Rate).HasPrecision(24, 12);
        builder.Property(debt => debt.Note).HasMaxLength(Debt.NoteMaxLength);

        builder.HasIndex(debt => new { debt.UserId, debt.Date });
        builder.HasIndex(debt => debt.AccountId);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(debt => debt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(debt => debt.AccountId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
