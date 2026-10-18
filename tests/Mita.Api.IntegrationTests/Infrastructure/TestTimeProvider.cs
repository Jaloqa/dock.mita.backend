namespace Mita.Api.IntegrationTests.Infrastructure;

public sealed class TestTimeProvider : TimeProvider
{
    private TimeSpan offset;

    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow + offset;

    public void Advance(TimeSpan by) => offset += by;
}
