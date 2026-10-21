using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mita.Domain.Tags;
using Mita.Domain.Transactions;

namespace Mita.Infrastructure.Persistence.Configurations;

public sealed class TransactionTagConfiguration : IEntityTypeConfiguration<TransactionTag>
{
    public void Configure(EntityTypeBuilder<TransactionTag> builder)
    {
        builder.ToTable("transaction_tags");
        builder.HasKey(row => new { row.TransactionId, row.TagId });

        builder.HasIndex(row => row.TagId);

        builder.HasOne<Transaction>()
            .WithMany(transaction => transaction.Tags)
            .HasForeignKey(row => row.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(row => row.Tag)
            .WithMany()
            .HasForeignKey(row => row.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
