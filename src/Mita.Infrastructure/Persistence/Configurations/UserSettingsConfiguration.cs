using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mita.Domain.Users;
using Mita.Infrastructure.Identity;

namespace Mita.Infrastructure.Persistence.Configurations;

public sealed class UserSettingsConfiguration : IEntityTypeConfiguration<UserSettings>
{
    public void Configure(EntityTypeBuilder<UserSettings> builder)
    {
        builder.ToTable("user_settings");
        builder.HasKey(settings => settings.UserId);

        builder.HasOne<AppUser>()
            .WithOne()
            .HasForeignKey<UserSettings>(settings => settings.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(settings => settings.BaseCurrency).HasConversion<string>().HasMaxLength(3);
        builder.Property(settings => settings.Locale).HasConversion<string>().HasMaxLength(2);
        builder.Property(settings => settings.Theme).HasConversion<string>().HasMaxLength(10);
        builder.Property(settings => settings.MonthStartDay);
        builder.Ignore(settings => settings.MonthStart);

        builder.ComplexProperty(settings => settings.Notifications, notifications =>
        {
            notifications.Property(prefs => prefs.BudgetLimit).HasColumnName("NotifyBudgetLimit");
            notifications.Property(prefs => prefs.DailyReminder).HasColumnName("NotifyDailyReminder");
            notifications.Property(prefs => prefs.WeeklyReport).HasColumnName("NotifyWeeklyReport");
            notifications.Property(prefs => prefs.Goals).HasColumnName("NotifyGoals");
        });
    }
}
