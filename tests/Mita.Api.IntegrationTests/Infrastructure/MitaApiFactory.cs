using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Mita.Api.IntegrationTests.Infrastructure;

public sealed class MitaApiFactory(PostgresFixture postgres, RedisFixture redis) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Mita"] = postgres.ConnectionString,
                ["ConnectionStrings:Redis"] = redis.ConnectionString,
                ["Database:ApplyMigrationsOnStartup"] = "true",
                ["Jwt:Issuer"] = TestTokens.Issuer,
                ["Jwt:Audience"] = TestTokens.Audience,
                ["Jwt:SigningKey"] = TestTokens.SigningKey
            }));
    }
}
