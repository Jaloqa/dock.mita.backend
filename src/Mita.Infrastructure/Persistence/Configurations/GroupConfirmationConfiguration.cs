using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mita.Domain.Accounts;
using Mita.Domain.Groups;
using Mita.Infrastructure.Identity;

namespace Mita.Infrastructure.Persistence.Configurations;

public sealed class GroupConfirmationConfiguration : IEntityTypeConfiguration<GroupConfirmation>
{
    public void Configure(EntityTypeBuilder<GroupConfirmation> builder)
    {
        builder.ToTable("group_confirmations");
        builder.HasKey(confirmation => confirmation.Id);
        builder.Property(confirmation => confirmation.Id).ValueGeneratedNever();

        builder.Property(confirmation => confirmation.Kind).HasConversion<string>().HasMaxLength(10);
        builder.Property(confirmation => confirmation.HandledTerms).HasColumnType("jsonb");
        builder.Property(confirmation => confirmation.ConfirmedTerms).HasColumnType("jsonb");

        builder.HasIndex(confirmation => new { confirmation.UserId, confirmation.Kind, confirmation.EntryId }).IsUnique();
        builder.HasIndex(confirmation => confirmation.AccountId);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(confirmation => confirmation.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(confirmation => confirmation.AccountId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
