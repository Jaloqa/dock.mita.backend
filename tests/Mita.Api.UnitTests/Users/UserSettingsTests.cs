using Mita.Domain.Money;
using Mita.Domain.Users;
using Shouldly;

namespace Mita.Api.UnitTests.Users;

public sealed class UserSettingsTests
{
    [Fact]
    public void Should_UseFrontendDefaults_When_CreatingDefaultSettings()
    {
        var userId = Guid.NewGuid();

        var settings = UserSettings.CreateDefault(userId, Locale.Pl);

        settings.UserId.ShouldBe(userId);
        settings.BaseCurrency.ShouldBe(Currency.PLN);
        settings.Locale.ShouldBe(Locale.Pl);
        settings.MonthStart.ShouldBe(MonthStart.Day(1));
        settings.MonthStart.Value.ShouldBe(1);
        settings.Theme.ShouldBe(ThemePreference.System);
        settings.Notifications.ShouldBe(new NotificationPrefs(true, true, false, true));
    }

    [Fact]
    public void Should_RejectNonBaseCurrency_When_SettingBaseCurrency()
    {
        var settings = UserSettings.CreateDefault(Guid.NewGuid(), Locale.En);

        Should.Throw<ArgumentException>(() => settings.BaseCurrency = Currency.JPY);
        settings.BaseCurrency.ShouldBe(Currency.PLN);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(29)]
    public void Should_RejectMonthStart_When_OutOfRange(int day) =>
        Should.Throw<ArgumentOutOfRangeException>(() => MonthStart.Day(day));

    [Fact]
    public void Should_RoundTripLastWorkingDay_When_ChangingMonthStart()
    {
        var settings = UserSettings.CreateDefault(Guid.NewGuid(), Locale.En);

        settings.MonthStart = MonthStart.LastWorkingDay;

        settings.MonthStart.IsLastWorkingDay.ShouldBeTrue();
        settings.MonthStartDay.ShouldBeNull();
    }
}
