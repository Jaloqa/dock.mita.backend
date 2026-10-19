using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mita.Domain.Accounts;
using Mita.Domain.Goals;
using Mita.Infrastructure.Identity;

namespace Mita.Infrastructure.Persistence.Configurations;

public sealed class GoalConfiguration : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> builder)
    {
        builder.ToTable("goals", table => table.HasCheckConstraint("CK_goals_target_positive", "\"Target\" > 0"));
        builder.HasKey(goal => goal.Id);
        builder.Property(goal => goal.Id).ValueGeneratedNever();

        builder.Property(goal => goal.Name).HasMaxLength(Goal.NameMaxLength).IsRequired();
        builder.Property(goal => goal.Currency).HasConversion<string>().HasMaxLength(3);
        builder.Property(goal => goal.Deadline).HasConversion(PeriodKeyConverters.Optional).HasMaxLength(7);

        builder.HasIndex(goal => goal.UserId);
        builder.HasIndex(goal => goal.AccountId);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(goal => goal.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(goal => goal.AccountId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
