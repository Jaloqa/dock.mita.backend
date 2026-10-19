using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Features.Auth;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Categories;
using Mita.Infrastructure.Identity;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Users;

public sealed class MeTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string MePath = "/v1/me";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_ReturnCurrentUser()
    {
        using var client = new AuthClient(factory);
        var credentials = AuthClient.NewCredentials();
        using var registered = await client.RegisterAsync(credentials, ct: Ct);
        var created = await registered.Content.ReadFromJsonAsync<JsonElement>(Ct);

        using var response = await client.Http.GetAsync(MePath, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.EnumerateObject().Select(property => property.Name)
            .ShouldBe(["id", "name", "email", "createdAt"], ignoreOrder: true);
        body.GetProperty("id").GetGuid().ShouldBe(created.GetProperty("id").GetGuid());
        body.GetProperty("name").GetString().ShouldBe(credentials.Name);
        body.GetProperty("email").GetString().ShouldBe(credentials.Email);
        body.GetProperty("createdAt").GetString().ShouldBe(created.GetProperty("createdAt").GetString());
        body.GetProperty("createdAt").GetString()!.ShouldMatch(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$");
    }

    [Fact]
    public async Task Should_Return401_When_NotAuthenticated()
    {
        using var client = new AuthClient(factory);

        using var response = await client.Http.GetAsync(MePath, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_UpdateNameAndEmail()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        using (var primed = await client.Http.GetAsync(MePath, Ct))
        {
            primed.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var newEmail = $"{Guid.NewGuid():N}@updated.example.com";
        using var response = await UpdateAsync(client, new { name = "  Anna Kowalska ", email = $" {newEmail} " });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("name").GetString().ShouldBe("Anna Kowalska");
        body.GetProperty("email").GetString().ShouldBe(newEmail);

        var me = await GetMeAsync(client);
        me.GetProperty("name").GetString().ShouldBe("Anna Kowalska");
        me.GetProperty("email").GetString().ShouldBe(newEmail);

        await using var scope = factory.Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AsNoTracking()
            .SingleAsync(row => row.Id == me.GetProperty("id").GetGuid(), Ct);
        user.UserName.ShouldBe(newEmail);
        user.NormalizedEmail.ShouldBe(newEmail.ToUpperInvariant());
        user.NormalizedUserName.ShouldBe(newEmail.ToUpperInvariant());
    }

    [Fact]
    public async Task Should_ReturnFieldErrors_When_UpdateInvalid()
    {
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);

        var empty = await UpdateErrorsAsync(client, new { });
        empty["name"].ShouldBe(["validation.required"]);
        empty["email"].ShouldBe(["validation.required"]);

        var invalid = await UpdateErrorsAsync(client, new { name = new string('a', 41), email = "anna@" });
        invalid["name"].ShouldBe(["validation.nameTooLong"]);
        invalid["email"].ShouldBe(["validation.invalidEmail"]);

        var me = await GetMeAsync(client);
        me.GetProperty("name").GetString().ShouldBe(credentials.Name);
        me.GetProperty("email").GetString().ShouldBe(credentials.Email);
    }

    [Fact]
    public async Task Should_ReturnEmailTaken_When_OtherUserHasEmailInDifferentCase()
    {
        using var other = new AuthClient(factory);
        var taken = await other.RegisterNewAsync(Ct);
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);

        var errors = await UpdateErrorsAsync(
            client,
            new { name = "   ", email = " " + taken.Email.ToUpperInvariant() + " " });

        errors["email"].ShouldBe(["validation.emailTaken"]);
        errors["name"].ShouldBe(["validation.required"]);
        (await GetMeAsync(client)).GetProperty("email").GetString().ShouldBe(credentials.Email);
    }

    [Fact]
    public async Task Should_AllowSameEmail_When_ItIsOwnEmailInDifferentCase()
    {
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);
        var stampBefore = await SecurityStampAsync(credentials.Email);
        var upper = credentials.Email.ToUpperInvariant();

        using var response = await UpdateAsync(client, new { name = "Anna", email = upper });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("email").GetString().ShouldBe(upper);
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
        (await SecurityStampAsync(credentials.Email)).ShouldBe(stampBefore);
        (await GetMeAsync(client)).GetProperty("email").GetString().ShouldBe(upper);
        using var refreshed = await client.RefreshAsync(Ct);
        refreshed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Should_KeepSession_When_EmailChanged()
    {
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);
        var stampBefore = await SecurityStampAsync(credentials.Email);
        var oldRefresh = client.Cookie("mita_rt", "/v1/auth/")!.Value;
        var oldAccess = client.Cookie("mita_at")!.Value;
        var newEmail = $"{Guid.NewGuid():N}@moved.example.com";

        using (var response = await UpdateAsync(client, new { name = credentials.Name, email = newEmail }))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var cookies = response.Headers.GetValues("Set-Cookie").ToList();
            cookies.ShouldContain(cookie => cookie.StartsWith("mita_at=", StringComparison.Ordinal));
            cookies.ShouldContain(cookie => cookie.StartsWith("mita_rt=", StringComparison.Ordinal));
            cookies.ShouldContain(cookie => cookie.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
        }

        client.Cookie("mita_at")!.Value.ShouldNotBe(oldAccess);
        client.Cookie("mita_rt", "/v1/auth/")!.Value.ShouldNotBe(oldRefresh);
        (await SecurityStampAsync(newEmail)).ShouldNotBe(stampBefore);
        (await GetMeAsync(client)).GetProperty("email").GetString().ShouldBe(newEmail);
        using (var refreshed = await client.RefreshAsync(Ct))
        {
            refreshed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await GetMeAsync(client)).GetProperty("email").GetString().ShouldBe(newEmail);

        using var stale = new AuthClient(factory);
        stale.SetRefreshCookie(oldRefresh);
        using (var staleRefresh = await stale.RefreshAsync(Ct))
        {
            await RefreshTests.AssertSessionExpiredAsync(staleRefresh);
        }

        using var login = new AuthClient(factory);
        using (var oldLogin = await login.LoginAsync(credentials.Email, credentials.Password, Ct))
        {
            oldLogin.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using var newLogin = await login.LoginAsync(newEmail, credentials.Password, Ct);
        newLogin.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_DeleteAccount_When_PasswordCorrect()
    {
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);
        var userId = (await GetMeAsync(client)).GetProperty("id").GetGuid();

        using (var response = await DeleteAsync(client, new { password = credentials.Password }))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            RefreshTests.AssertCookiesCleared(response);
        }

        client.Cookie("mita_at").ShouldBeNull();
        using var login = new AuthClient(factory);
        using (var loginResponse = await login.LoginAsync(credentials, Ct))
        {
            loginResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Users.AnyAsync(row => row.Id == userId, Ct)).ShouldBeFalse();
        (await db.UserSettings.AnyAsync(row => row.UserId == userId, Ct)).ShouldBeFalse();
        (await db.RefreshTokens.AnyAsync(row => row.UserId == userId, Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Should_DeleteUserWithAccountsAndTransfers()
    {
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);
        var userId = (await GetMeAsync(client)).GetProperty("id").GetGuid();
        var from = await TestData.AddAccountAsync(factory, userId, openingBalance: 10_000);
        var to = await TestData.AddAccountAsync(factory, userId);
        await TestData.AddTransferAsync(factory, from, to, 2_500);

        using (var response = await DeleteAsync(client, new { password = credentials.Password }))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Accounts.AnyAsync(row => row.UserId == userId, Ct)).ShouldBeFalse();
        (await db.Transfers.AnyAsync(row => row.UserId == userId, Ct)).ShouldBeFalse();
        (await db.AccountMovements.AnyAsync(row => row.UserId == userId, Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Should_DeleteUserWithCategoriesAndTransactions()
    {
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);
        var userId = (await GetMeAsync(client)).GetProperty("id").GetGuid();
        var account = await TestData.AddAccountAsync(factory, userId, openingBalance: 10_000);
        var expense = await TestData.AddCategoryAsync(factory, userId);
        var income = await TestData.AddCategoryAsync(factory, userId, CategoryKind.Income);
        await TestData.AddTransactionAsync(factory, account, expense, 1_200);
        await TestData.AddTransactionAsync(factory, account, income, 500);

        using (var response = await DeleteAsync(client, new { password = credentials.Password }))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Categories.AnyAsync(row => row.UserId == userId, Ct)).ShouldBeFalse();
        (await db.Transactions.AnyAsync(row => row.UserId == userId, Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Should_DeleteAccount_When_PasswordCorrectAfterWrongAttempts()
    {
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);
        var userId = (await GetMeAsync(client)).GetProperty("id").GetGuid();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var failed = await DeleteAsync(client, new { password = "wrong-password-123" });
            failed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        using (var response = await DeleteAsync(client, new { password = credentials.Password }))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        using var login = new AuthClient(factory);
        using (var loginResponse = await login.LoginAsync(credentials, Ct))
        {
            loginResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Users.AnyAsync(row => row.Id == userId, Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Should_DeleteAccount_When_AccountChangesWhilePasswordIsChecked()
    {
        var hasher = new StampChangingPasswordHasher(factory.Services);
        await using var racing = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddScoped<IPasswordHasher<AppUser>>(_ => hasher)));
        using var client = new AuthClient(racing);
        var credentials = await client.RegisterNewAsync(Ct);
        var userId = (await GetMeAsync(client)).GetProperty("id").GetGuid();
        hasher.Arm();

        using (var response = await DeleteAsync(client, new { password = credentials.Password }))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        hasher.Fired.ShouldBeTrue();
        await using var scope = factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AnyAsync(row => row.Id == userId, Ct))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task Should_RejectDelete_When_PasswordWrong()
    {
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);

        using var response = await DeleteAsync(client, new { password = "not-the-password" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!["password"]
            .ShouldBe(["validation.invalidPassword"]);
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
        (await GetMeAsync(client)).GetProperty("email").GetString().ShouldBe(credentials.Email);
    }

    [Fact]
    public async Task Should_RequirePassword_When_DeleteWithoutPassword()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var response = await DeleteAsync(client, new { });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!["password"]
            .ShouldBe(["validation.required"]);
    }

    [Fact]
    public async Task Should_Return403_When_DeleteWithoutXsrfHeader()
    {
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);

        using var request = new HttpRequestMessage(HttpMethod.Delete, MePath)
        {
            Content = JsonContent.Create(new { password = credentials.Password })
        };
        using var response = await client.Http.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await GetMeAsync(client)).GetProperty("email").GetString().ShouldBe(credentials.Email);
    }

    [Fact]
    public async Task Should_Return403_When_UpdateWithoutXsrfHeader()
    {
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);

        using var request = new HttpRequestMessage(HttpMethod.Put, MePath)
        {
            Content = JsonContent.Create(new { name = "Changed Name", email = credentials.Email })
        };
        using var response = await client.Http.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await GetMeAsync(client)).GetProperty("name").GetString().ShouldBe(credentials.Name);
    }

    [Fact]
    public async Task Should_LockAccount_When_DeletePasswordWrongFiveTimes()
    {
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var failed = await DeleteAsync(client, new { password = "wrong-password-123" });
            failed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        using var lockedDelete = await DeleteAsync(client, new { password = credentials.Password });
        lockedDelete.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var deleteProblem = await lockedDelete.Content.ReadFromJsonAsync<JsonElement>(Ct);
        deleteProblem.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!["password"]
            .ShouldBe(["validation.invalidPassword"]);

        using var other = new AuthClient(factory);
        using var login = await other.LoginAsync(credentials, Ct);

        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var problem = await login.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("code").GetString().ShouldBe("auth.invalidCredentials");
        other.Cookie("mita_at").ShouldBeNull();
    }

    [Fact]
    public void Should_RateLimitDelete_When_Mapped()
    {
        var endpoint = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(candidate => candidate.RoutePattern.RawText == "/v{version:apiVersion}/me"
                && candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("DELETE") == true);

        endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>().ShouldNotBeNull().PolicyName.ShouldBe("auth");
    }

    private static async Task<JsonElement> GetMeAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync(MePath, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static Task<HttpResponseMessage> UpdateAsync(AuthClient client, object body) =>
        SendAsync(client, HttpMethod.Put, body);

    private static Task<HttpResponseMessage> DeleteAsync(AuthClient client, object body) =>
        SendAsync(client, HttpMethod.Delete, body);

    private static async Task<HttpResponseMessage> SendAsync(AuthClient client, HttpMethod method, object body)
    {
        using var request = client.WithXsrf(new HttpRequestMessage(method, MePath) { Content = JsonContent.Create(body) });
        return await client.Http.SendAsync(request, Ct);
    }

    private static async Task<Dictionary<string, string[]>> UpdateErrorsAsync(AuthClient client, object body)
    {
        using var response = await UpdateAsync(client, body);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return problem.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!;
    }

    private async Task<string?> SecurityStampAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var normalized = email.ToUpperInvariant();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AsNoTracking()
            .Where(row => row.NormalizedEmail == normalized)
            .Select(row => row.SecurityStamp)
            .SingleAsync(Ct);
    }

    private sealed class StampChangingPasswordHasher(IServiceProvider services) : IPasswordHasher<AppUser>
    {
        private readonly PasswordHasher<AppUser> inner = new();
        private int armed;

        public bool Fired { get; private set; }

        public void Arm() => Interlocked.Exchange(ref armed, 1);

        public string HashPassword(AppUser user, string password) => inner.HashPassword(user, password);

        public PasswordVerificationResult VerifyHashedPassword(AppUser user, string hashedPassword, string providedPassword)
        {
            if (Interlocked.Exchange(ref armed, 0) == 1)
            {
                using var scope = services.CreateScope();
                scope.ServiceProvider.GetRequiredService<AppDbContext>().Users
                    .Where(row => row.Id == user.Id)
                    .ExecuteUpdate(set => set.SetProperty(row => row.ConcurrencyStamp, Guid.NewGuid().ToString()));
                Fired = true;
            }

            return inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
        }
    }
}
