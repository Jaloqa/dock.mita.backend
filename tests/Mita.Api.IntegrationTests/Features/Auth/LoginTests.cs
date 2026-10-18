using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Infrastructure.Persistence;
using Serilog.Core;
using Serilog.Events;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Auth;

public sealed class LoginTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_Login_When_CredentialsValid()
    {
        TestCredentials credentials;
        using (var registering = new AuthClient(factory))
        {
            credentials = await registering.RegisterNewAsync(Ct);
        }

        using var client = new AuthClient(factory);
        using var response = await client.LoginAsync(" " + credentials.Email.ToUpperInvariant() + " ", credentials.Password, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.EnumerateObject().Select(property => property.Name)
            .ShouldBe(["id", "name", "email", "createdAt"], ignoreOrder: true);
        body.GetProperty("email").GetString().ShouldBe(credentials.Email);
        client.Cookie("mita_at").ShouldNotBeNull();
        client.Cookie("mita_rt", "/v1/auth/").ShouldNotBeNull();
        client.Xsrf.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Should_Return401SameBody_When_PasswordWrongOrUserUnknown()
    {
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);

        using var anonymous = new AuthClient(factory);
        using var wrongPassword = await anonymous.LoginAsync(credentials.Email, "wrong-password-123", Ct);
        using var unknownUser = await anonymous.LoginAsync($"{Guid.NewGuid():N}@example.com", "wrong-password-123", Ct);

        var first = await AssertInvalidCredentialsAsync(wrongPassword);
        var second = await AssertInvalidCredentialsAsync(unknownUser);
        WithoutCorrelationId(first).ShouldBe(WithoutCorrelationId(second));
        anonymous.Cookie("mita_at").ShouldBeNull();
    }

    [Fact]
    public async Task Should_ReturnRequired_When_LoginFieldsMissing()
    {
        using var client = new AuthClient(factory);

        using var response = await client.LoginAsync("  ", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        errors.GetProperty("email")[0].GetString().ShouldBe("validation.required");
        errors.GetProperty("password")[0].GetString().ShouldBe("validation.required");
    }

    [Fact]
    public async Task Should_LockAfterFiveFailures()
    {
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);

        using var attacker = new AuthClient(factory);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var failed = await attacker.LoginAsync(credentials.Email, "wrong-password-123", Ct);
            await AssertInvalidCredentialsAsync(failed);
        }

        using var locked = await attacker.LoginAsync(credentials, Ct);

        await AssertInvalidCredentialsAsync(locked);
        attacker.Cookie("mita_at").ShouldBeNull();
    }

    [Fact]
    public async Task Should_LockOut_When_WrongPasswordsArriveInParallel()
    {
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);

        using var attacker = new AuthClient(factory);
        var statuses = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            using var failed = await attacker.LoginAsync(credentials.Email, "wrong-password-123", Ct);
            return failed.StatusCode;
        }));
        statuses.ShouldAllBe(status => status == HttpStatusCode.Unauthorized);

        using var locked = await attacker.LoginAsync(credentials, Ct);

        await AssertInvalidCredentialsAsync(locked);
        await using var scope = factory.Services.CreateAsyncScope();
        var normalized = credentials.Email.ToUpperInvariant();
        var user = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AsNoTracking()
            .SingleAsync(row => row.NormalizedEmail == normalized, Ct);
        user.LockoutEnd.ShouldNotBeNull();
        user.LockoutEnd.Value.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(14));
    }

    [Fact]
    public async Task Should_ResetFailures_When_LoginSucceeds()
    {
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);

        for (var round = 0; round < 2; round++)
        {
            for (var attempt = 0; attempt < 4; attempt++)
            {
                using var failed = await client.LoginAsync(credentials.Email, "wrong-password-123", Ct);
                failed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            }

            using var success = await client.LoginAsync(credentials, Ct);
            success.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Should_Return429_When_TooManyAuthRequests()
    {
        await using var limited = factory.WithSettings(
            new Dictionary<string, string?> { ["RateLimiting:AuthPermitLimit"] = "10" });
        using var client = new AuthClient(limited);

        for (var request = 0; request < 10; request++)
        {
            using var allowed = await client.LoginAsync("nobody@example.com", "wrong-password-123", Ct);
            allowed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using var rejected = await client.LoginAsync("nobody@example.com", "wrong-password-123", Ct);

        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        rejected.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await rejected.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("status").GetInt32().ShouldBe(429);
        problem.GetProperty("title").GetString().ShouldBe("Too many requests");
        problem.GetProperty("correlationId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Should_NotLogPassword()
    {
        var sink = new CapturingSink();
        await using var logged = factory
            .WithSettings(new Dictionary<string, string?>
            {
                ["Serilog:MinimumLevel:Default"] = "Verbose",
                ["Serilog:MinimumLevel:Override:Microsoft.AspNetCore"] = "Verbose",
                ["Serilog:MinimumLevel:Override:System"] = "Verbose"
            })
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                services => services.AddSingleton<ILogEventSink>(sink)));
        using var client = new AuthClient(logged);
        var credentials = AuthClient.NewCredentials() with { Password = $"secret-{Guid.NewGuid():N}" };

        using (var registered = await client.RegisterAsync(credentials, ct: Ct))
        {
            registered.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        var refreshToken = client.Cookie("mita_rt", "/v1/auth/")!.Value;
        var accessToken = client.Cookie("mita_at")!.Value;
        using (var wrong = await client.LoginAsync(credentials.Email, credentials.Password + "x", Ct))
        {
            wrong.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using (var right = await client.LoginAsync(credentials, Ct))
        {
            right.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        sink.Events.ShouldNotBeEmpty();
        var rendered = string.Join('\n', sink.Events.Select(Render));
        rendered.ShouldNotContain(credentials.Password);
        rendered.ShouldNotContain(refreshToken);
        rendered.ShouldNotContain(accessToken);
    }

    [Fact]
    public async Task Should_LogRequestCompletion_When_RequestHandled()
    {
        var sink = new CapturingSink();
        await using var logged = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<ILogEventSink>(sink)));
        using var client = new AuthClient(logged);

        using (var response = await client.LoginAsync("nobody@example.com", "wrong-password-123", Ct))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        var completion = sink.Events.SingleOrDefault(logEvent =>
            logEvent.Properties.TryGetValue("RequestPath", out var path)
            && path.ToString() == $"\"{AuthClient.LoginPath}\""
            && logEvent.Properties.ContainsKey("StatusCode"));
        completion.ShouldNotBeNull();
        completion.Properties["StatusCode"].ToString().ShouldBe("401");
        completion.Properties["RequestMethod"].ToString().ShouldBe("\"POST\"");
    }

    [Fact]
    public async Task Should_AllowConfiguredOrigin_When_Preflight()
    {
        await using var configured = factory.WithSettings(
            new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = "http://localhost:3000" });
        using var client = configured.CreateClient();

        using var allowed = await client.SendAsync(Preflight("http://localhost:3000"), Ct);
        using var denied = await client.SendAsync(Preflight("https://evil.example.com"), Ct);

        allowed.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe(["http://localhost:3000"]);
        allowed.Headers.GetValues("Access-Control-Allow-Credentials").ShouldBe(["true"]);
        var allowedHeaders = string.Join(',', allowed.Headers.GetValues("Access-Control-Allow-Headers"));
        allowedHeaders.ShouldContain("X-XSRF-TOKEN", Case.Insensitive);
        allowedHeaders.ShouldContain("Content-Type", Case.Insensitive);
        denied.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, AuthClient.LoginPath);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type,x-xsrf-token");
        return request;
    }

    private static async Task<JsonElement> AssertInvalidCredentialsAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("title").GetString().ShouldBe("Unauthorized");
        problem.GetProperty("code").GetString().ShouldBe("auth.invalidCredentials");
        return problem;
    }

    private static string WithoutCorrelationId(JsonElement problem) =>
        JsonSerializer.Serialize(problem.EnumerateObject()
            .Where(property => property.Name is not ("correlationId" or "traceId"))
            .ToDictionary(property => property.Name, property => property.Value.ToString()));

    private static string Render(LogEvent logEvent) =>
        string.Join(
            ' ',
            [logEvent.RenderMessage(CultureInfo.InvariantCulture),logEvent.MessageTemplate.Text, logEvent.Exception?.ToString() ?? string.Empty,
                .. logEvent.Properties.Select(property => $"{property.Key}={property.Value}")]);

    private sealed class CapturingSink : ILogEventSink
    {
        private readonly ConcurrentQueue<LogEvent> events = new();

        public IReadOnlyCollection<LogEvent> Events => events;

        public void Emit(LogEvent logEvent) => events.Enqueue(logEvent);
    }
}
