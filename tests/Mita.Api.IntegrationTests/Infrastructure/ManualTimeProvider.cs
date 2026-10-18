namespace Mita.Api.IntegrationTests.Infrastructure;

public sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private readonly Lock gate = new();
    private readonly List<ManualTimer> timers = [];
    private TimeSpan offset;

    public ManualTimeProvider()
        : this(DateTimeOffset.UtcNow)
    {
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (gate)
        {
            return start + offset;
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    public void Advance(TimeSpan by)
    {
        List<ManualTimer> due;
        lock (gate)
        {
            offset += by;
            var now = start + offset;
            due = timers.Where(timer => timer.DueAt <= now).ToList();
            foreach (var timer in due)
            {
                timers.Remove(timer);
            }
        }

        foreach (var timer in due)
        {
            timer.Fire();
        }
    }

    private void Schedule(ManualTimer timer, TimeSpan dueTime)
    {
        lock (gate)
        {
            timers.Remove(timer);
            if (dueTime != Timeout.InfiniteTimeSpan)
            {
                timer.DueAt = start + offset + dueTime;
                timers.Add(timer);
            }
        }
    }

    private void Unschedule(ManualTimer timer)
    {
        lock (gate)
        {
            timers.Remove(timer);
        }
    }

    private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        private TimeSpan period = Timeout.InfiniteTimeSpan;

        public DateTimeOffset DueAt { get; set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            this.period = period;
            owner.Schedule(this, dueTime);
            return true;
        }

        public void Fire()
        {
            callback(state);
            if (period != Timeout.InfiniteTimeSpan && period > TimeSpan.Zero)
            {
                owner.Schedule(this, period);
            }
        }

        public void Dispose() => owner.Unschedule(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
