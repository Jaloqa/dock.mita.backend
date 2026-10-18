using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mita.Domain.Accounts;
using Mita.Infrastructure.Identity;

namespace Mita.Infrastructure.Persistence.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");
        builder.HasKey(account => account.Id);
        builder.Property(account => account.Id).ValueGeneratedNever();

        builder.Property(account => account.Name).HasMaxLength(Account.NameMaxLength).IsRequired();
        builder.Property(account => account.Type).HasConversion<string>().HasMaxLength(10);
        builder.Property(account => account.Currency).HasConversion<string>().HasMaxLength(3);
        builder.Property(account => account.CardLast4).HasMaxLength(4);

        builder.HasIndex(account => new { account.UserId, account.CreatedAt });

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(account => account.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
