namespace Mita.Domain.Groups;

public sealed class GroupAccountLeg
{
    private GroupAccountLeg()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid ConfirmationId { get; private set; }

    public Guid AccountId { get; private set; }

    public DateOnly Date { get; private set; }

    public long AccountAmount { get; private set; }

    public static GroupAccountLeg Create(Guid userId, Guid confirmationId, Guid accountId, DateOnly date, long accountAmount)
    {
        if (accountAmount == 0)
        {
            throw new ArgumentException("A leg must move money.", nameof(accountAmount));
        }

        return new GroupAccountLeg
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            ConfirmationId = confirmationId,
            AccountId = accountId,
            Date = date,
            AccountAmount = accountAmount
        };
    }
}
