using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mita.Domain.Groups;
using Mita.Domain.Transactions;

namespace Mita.Infrastructure.Persistence.Configurations;

public sealed class GroupConfirmationTransactionConfiguration : IEntityTypeConfiguration<GroupConfirmationTransaction>
{
    public void Configure(EntityTypeBuilder<GroupConfirmationTransaction> builder)
    {
        builder.ToTable("group_confirmation_transactions");
        builder.HasKey(link => link.TransactionId);
        builder.Property(link => link.TransactionId).ValueGeneratedNever();

        builder.HasIndex(link => link.ConfirmationId);

        builder.HasOne<Transaction>()
            .WithMany()
            .HasForeignKey(link => link.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<GroupConfirmation>()
            .WithMany()
            .HasForeignKey(link => link.ConfirmationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
