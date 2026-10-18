using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Mita.Infrastructure.Persistence;

namespace Mita.Infrastructure.Identity;

public sealed class LockoutCounter(AppDbContext db, IOptions<IdentityOptions> identityOptions, TimeProvider time)
{
    // UserManager.AccessFailedAsync is a read-modify-write guarded by the concurrency stamp, so parallel failures
    // overwrite each other and never reach the lockout threshold. A single UPDATE lets the database serialise them.
    public Task<int> RegisterFailureAsync(Guid userId, CancellationToken cancellationToken)
    {
        var lockout = identityOptions.Value.Lockout;
        var threshold = lockout.MaxFailedAccessAttempts;
        var lockoutEnd = time.GetUtcNow() + lockout.DefaultLockoutTimeSpan;
        var stamp = Guid.NewGuid().ToString();

        return db.Users
            .Where(row => row.Id == userId)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(
                        row => row.LockoutEnd,
                        row => row.LockoutEnabled && row.AccessFailedCount + 1 >= threshold ? lockoutEnd : row.LockoutEnd)
                    .SetProperty(
                        row => row.AccessFailedCount,
                        row => row.LockoutEnabled && row.AccessFailedCount + 1 >= threshold ? 0 : row.AccessFailedCount + 1)
                    .SetProperty(row => row.ConcurrencyStamp, stamp),
                cancellationToken);
    }

    public Task<int> ResetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var stamp = Guid.NewGuid().ToString();

        return db.Users
            .Where(row => row.Id == userId && row.AccessFailedCount > 0)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(row => row.AccessFailedCount, 0)
                    .SetProperty(row => row.ConcurrencyStamp, stamp),
                cancellationToken);
    }
}
