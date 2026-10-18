namespace Mita.Domain.Users;

public readonly record struct MonthStart
{
    public const int MinDay = 1;
    public const int MaxDay = 28;

    private MonthStart(int? value) => Value = value;

    public static MonthStart LastWorkingDay => new(null);

    public int? Value { get; }

    public bool IsLastWorkingDay => Value is null;

    public static MonthStart Day(int day) =>
        day is >= MinDay and <= MaxDay
            ? new MonthStart(day)
            : throw new ArgumentOutOfRangeException(nameof(day), day, $"Day must be between {MinDay} and {MaxDay}.");
}
