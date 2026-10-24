using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mita.Domain.Accounts;
using Mita.Domain.Groups;
using Mita.Infrastructure.Identity;

namespace Mita.Infrastructure.Persistence.Configurations;

public sealed class GroupAccountLegConfiguration : IEntityTypeConfiguration<GroupAccountLeg>
{
    public void Configure(EntityTypeBuilder<GroupAccountLeg> builder)
    {
        builder.ToTable(
            "group_account_legs",
            table => table.HasCheckConstraint("CK_group_account_legs_amount_not_zero", "\"AccountAmount\" <> 0"));
        builder.HasKey(leg => leg.Id);
        builder.Property(leg => leg.Id).ValueGeneratedNever();

        builder.HasIndex(leg => leg.UserId);
        builder.HasIndex(leg => leg.ConfirmationId);
        builder.HasIndex(leg => leg.AccountId);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(leg => leg.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<GroupConfirmation>()
            .WithMany()
            .HasForeignKey(leg => leg.ConfirmationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(leg => leg.AccountId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
