namespace Mita.Infrastructure.Persistence.ReadModels;

public sealed record AccountMovement(Guid UserId, Guid AccountId, long Amount);
