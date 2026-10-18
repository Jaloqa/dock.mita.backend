using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Mita.Api.IntegrationTests.Infrastructure;

public sealed class MitaApiFactory(PostgresFixture postgres, RedisFixture redis) : WebApplicationFactory<Program>
{
    public WebApplicationFactory<Program> WithSettings(IReadOnlyDictionary<string, string?> settings) =>
        WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(
            (_, configuration) => configuration.AddInMemoryCollection(settings)));

    public WebApplicationFactory<Program> WithIsolatedData() =>
        WithSettings(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Mita"] = postgres.NewDatabaseConnectionString(),
            ["Redis:InstanceName"] = $"mita-{Guid.NewGuid():N}:"
        });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Mita"] = postgres.ConnectionString,
                ["ConnectionStrings:Redis"] = redis.ConnectionString,
                ["Database:ApplyMigrationsOnStartup"] = "true",
                ["Rates:SyncEnabled"] = "false",
                ["Jwt:Issuer"] = TestTokens.Issuer,
                ["Jwt:Audience"] = TestTokens.Audience,
                ["Jwt:SigningKey"] = TestTokens.SigningKey,
                ["RateLimiting:AuthPermitLimit"] = "1000",
                ["RateLimiting:SessionPermitLimit"] = "1000"
            }));
    }
}
