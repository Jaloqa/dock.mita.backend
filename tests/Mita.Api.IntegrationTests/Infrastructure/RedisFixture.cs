using StackExchange.Redis;
using Testcontainers.Redis;

[assembly: AssemblyFixture(typeof(Mita.Api.IntegrationTests.Infrastructure.RedisFixture))]

namespace Mita.Api.IntegrationTests.Infrastructure;

public sealed class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer container = new RedisBuilder("redis:8.6.2-alpine").Build();
    private ConnectionMultiplexer? multiplexer;

    public string ConnectionString => container.GetConnectionString();

    public IConnectionMultiplexer Multiplexer =>
        multiplexer ?? throw new InvalidOperationException("The Redis fixture is not initialized.");

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync();
        multiplexer = await ConnectionMultiplexer.ConnectAsync(ConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        if (multiplexer is not null)
        {
            await multiplexer.DisposeAsync();
        }

        await container.DisposeAsync();
    }
}
