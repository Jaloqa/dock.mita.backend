using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mita.Domain.Accounts;
using Mita.Domain.Categories;
using Mita.Domain.Transactions;
using Mita.Infrastructure.Identity;

namespace Mita.Infrastructure.Persistence.Configurations;

public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable(
            "transactions",
            table =>
            {
                table.HasCheckConstraint("CK_transactions_amount_positive", "\"Amount\" > 0");
                table.HasCheckConstraint("CK_transactions_account_amount_positive", "\"AccountAmount\" > 0");
            });
        builder.HasKey(transaction => transaction.Id);
        builder.Property(transaction => transaction.Id).ValueGeneratedNever();

        builder.Property(transaction => transaction.Kind).HasConversion<string>().HasMaxLength(10);
        builder.Property(transaction => transaction.Title).HasMaxLength(Transaction.TitleMaxLength).IsRequired();
        builder.Property(transaction => transaction.Note).HasMaxLength(Transaction.NoteMaxLength);
        builder.Property(transaction => transaction.Currency).HasConversion<string>().HasMaxLength(3);
        builder.Property(transaction => transaction.Rate).HasPrecision(24, 12);

        builder.HasIndex(transaction => new { transaction.UserId, transaction.Date });
        builder.HasIndex(transaction => transaction.AccountId);
        builder.HasIndex(transaction => transaction.CategoryId);

        builder.Navigation(transaction => transaction.Tags).HasField("tags");

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(transaction => transaction.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(transaction => transaction.AccountId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(transaction => transaction.CategoryId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
