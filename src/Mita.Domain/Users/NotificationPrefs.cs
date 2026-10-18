namespace Mita.Domain.Users;

public sealed record NotificationPrefs(bool BudgetLimit, bool DailyReminder, bool WeeklyReport, bool Goals)
{
    public static NotificationPrefs Default { get; } = new(true, true, false, true);
}
