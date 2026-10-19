using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mita.Domain.Accounts;
using Mita.Domain.Goals;
using Mita.Infrastructure.Identity;

namespace Mita.Infrastructure.Persistence.Configurations;

public sealed class GoalContributionConfiguration : IEntityTypeConfiguration<GoalContribution>
{
    public void Configure(EntityTypeBuilder<GoalContribution> builder)
    {
        builder.ToTable("goal_contributions");
        builder.HasKey(contribution => contribution.Id);
        builder.Property(contribution => contribution.Id).ValueGeneratedNever();

        builder.Property(contribution => contribution.Currency).HasConversion<string>().HasMaxLength(3);
        builder.Property(contribution => contribution.BaseCurrency).HasConversion<string>().HasMaxLength(3);

        builder.HasIndex(contribution => new { contribution.UserId, contribution.Date });
        builder.HasIndex(contribution => contribution.GoalId);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(contribution => contribution.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Goal>()
            .WithMany()
            .HasForeignKey(contribution => contribution.GoalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(contribution => contribution.FromAccountId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Transfer>()
            .WithMany()
            .HasForeignKey(contribution => contribution.TransferId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
