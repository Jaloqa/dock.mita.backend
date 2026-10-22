using Mita.Domain.Common;
using Mita.Domain.Money;
using Mita.Domain.Periods;

namespace Mita.Domain.Goals;

public sealed class Goal : IAuditable
{
    public const int NameMaxLength = 40;

    private Goal()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public long Target { get; private set; }

    public long Saved { get; private set; }

    public Currency Currency { get; private set; }

    public PeriodKey? Deadline { get; private set; }

    public Guid? AccountId { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public static Goal Create(
        Guid userId,
        string name,
        long target,
        PeriodKey? deadline,
        Guid? accountId,
        Currency currency)
    {
        var goal = new Goal { Id = Guid.CreateVersion7(), UserId = userId, Currency = currency };
        goal.Update(name, target, deadline, accountId);
        return goal;
    }

    public void Update(string name, long target, PeriodKey? deadline, Guid? accountId)
    {
        Name = name;
        Target = target;
        Deadline = deadline;
        AccountId = accountId;
    }

    public void Rebase(Currency currency, decimal rate)
    {
        Target = Math.Max(1, MinorUnits.Convert(Target, rate, Currency, currency));
        Saved = MinorUnits.Convert(Saved, rate, Currency, currency);
        Currency = currency;
    }

    public void AddSaved(long amount) => Saved += amount;
}
