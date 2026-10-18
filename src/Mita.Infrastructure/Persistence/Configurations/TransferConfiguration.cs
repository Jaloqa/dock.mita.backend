using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mita.Domain.Accounts;
using Mita.Infrastructure.Identity;

namespace Mita.Infrastructure.Persistence.Configurations;

public sealed class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.ToTable(
            "transfers",
            table =>
            {
                table.HasCheckConstraint("CK_transfers_different_accounts", "\"FromAccountId\" <> \"ToAccountId\"");
                table.HasCheckConstraint("CK_transfers_from_amount_positive", "\"FromAmount\" > 0");
                table.HasCheckConstraint("CK_transfers_to_amount_positive", "\"ToAmount\" > 0");
            });
        builder.HasKey(transfer => transfer.Id);
        builder.Property(transfer => transfer.Id).ValueGeneratedNever();

        builder.Property(transfer => transfer.Rate).HasPrecision(18, 6);
        builder.Property(transfer => transfer.Note).HasMaxLength(Transfer.NoteMaxLength);

        builder.HasIndex(transfer => new { transfer.UserId, transfer.Date });
        builder.HasIndex(transfer => transfer.FromAccountId);
        builder.HasIndex(transfer => transfer.ToAccountId);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(transfer => transfer.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(transfer => transfer.FromAccountId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(transfer => transfer.ToAccountId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
