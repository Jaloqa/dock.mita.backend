using Mita.Domain.Common;
using Mita.Domain.Money;
using Mita.Domain.Periods;

namespace Mita.Domain.Budgets;

public sealed class Budget : IAuditable
{
    private Budget()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid CategoryId { get; private set; }

    public PeriodKey Period { get; private set; }

    public long Limit { get; private set; }

    public Currency Currency { get; private set; }

    public int Position { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public static Budget Create(Guid userId, Guid categoryId, PeriodKey period, long limit, Currency currency)
    {
        var budget = new Budget { Id = Guid.CreateVersion7(), UserId = userId };
        budget.Update(categoryId, period, limit, currency);
        return budget;
    }

    public void Update(Guid categoryId, PeriodKey period, long limit, Currency currency)
    {
        CategoryId = categoryId;
        Period = period;
        Limit = limit;
        Currency = currency;
    }

    public void MoveTo(Guid categoryId) => CategoryId = categoryId;

    public void Absorb(long limit, Currency currency)
    {
        Limit = limit;
        Currency = currency;
    }
}
