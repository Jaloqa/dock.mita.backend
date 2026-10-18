using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Infrastructure.Authentication;
using Mita.Infrastructure.Identity;
using Mita.Infrastructure.Persistence;
using Npgsql;
using Shouldly;

namespace Mita.Api.IntegrationTests.Authentication;

public sealed class TokenServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly JwtOptions Jwt = new()
    {
        Issuer = TestTokens.Issuer,
        Audience = TestTokens.Audience,
        SigningKey = TestTokens.SigningKey
    };

    private readonly TestTimeProvider time = new(DateTimeOffset.UtcNow);
    private string connectionString = string.Empty;
    private AppUser user = null!;

    public async ValueTask InitializeAsync()
    {
        connectionString = postgres.NewDatabaseConnectionString();
        await using var db = NewContext();
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);

        user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = "anna@example.com",
            NormalizedUserName = "ANNA@EXAMPLE.COM",
            Email = "anna@example.com",
            NormalizedEmail = "ANNA@EXAMPLE.COM",
            Name = "Anna",
            SecurityStamp = Guid.NewGuid().ToString("N")
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Should_IssueAccessTokenWithSubStampAndFifteenMinutes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = NewContext();

        var issued = await NewService(db).IssueAsync(user, null, ct);

        var token = new JsonWebTokenHandler().ReadJsonWebToken(issued.AccessToken);
        token.GetClaim("sub").Value.ShouldBe(user.Id.ToString());
        token.GetClaim("stamp").Value.ShouldBe(user.SecurityStamp);
        token.GetClaim("email").Value.ShouldBe(user.Email);
        token.GetClaim("name").Value.ShouldBe(user.Name);
        token.GetClaim("jti").Value.ShouldNotBeNullOrWhiteSpace();
        token.Issuer.ShouldBe(Jwt.Issuer);
        token.Audiences.ShouldBe([Jwt.Audience]);
        issued.AccessExpires.ShouldBe(time.GetUtcNow().AddMinutes(15));
        new DateTimeOffset(token.ValidTo).ShouldBe(issued.AccessExpires, TimeSpan.FromSeconds(1));
        issued.RefreshExpires.ShouldBe(time.GetUtcNow().AddDays(30));
    }

    [Fact]
    public async Task Should_StoreOnlyHash_When_IssuingRefreshToken()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = NewContext();

        var issued = await NewService(db).IssueAsync(user, null, ct);

        var stored = await NewContext().RefreshTokens.AsNoTracking().SingleAsync(ct);
        stored.TokenHash.ShouldNotBe(issued.RefreshToken);
        stored.TokenHash.ShouldNotContain(issued.RefreshToken);
        stored.TokenHash.Length.ShouldBe(64);
        stored.UserId.ShouldBe(user.Id);
        stored.RevokedAt.ShouldBeNull();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM refresh_tokens WHERE position(@raw in \"TokenHash\") > 0", connection);
        command.Parameters.AddWithValue("raw", issued.RefreshToken);
        ((long)(await command.ExecuteScalarAsync(ct))!).ShouldBe(0);
    }

    [Fact]
    public async Task Should_RotateAndRevokePrevious_When_Refreshing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = NewContext();
        var service = NewService(db);
        var first = await service.IssueAsync(user, null, ct);

        var outcome = await service.RefreshAsync(first.RefreshToken, ct);

        var rotated = outcome.ShouldBeOfType<RefreshOutcome.Rotated>();
        rotated.User.Id.ShouldBe(user.Id);
        rotated.Tokens.RefreshToken.ShouldNotBe(first.RefreshToken);
        var tokens = await NewContext().RefreshTokens.AsNoTracking().OrderBy(token => token.CreatedAt).ToListAsync(ct);
        tokens.Count.ShouldBe(2);
        var previous = tokens.Single(token => token.RevokedAt is not null);
        var current = tokens.Single(token => token.RevokedAt is null);
        previous.FamilyId.ShouldBe(current.FamilyId);
        previous.ReplacedByHash.ShouldBe(current.TokenHash);
    }

    [Fact]
    public async Task Should_RevokeWholeFamily_When_RevokedTokenIsReused()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = NewContext();
        var service = NewService(db);
        var first = await service.IssueAsync(user, null, ct);
        var rotated = (RefreshOutcome.Rotated)await service.RefreshAsync(first.RefreshToken, ct);
        time.Advance(TokenService.RotationGracePeriod + TimeSpan.FromSeconds(1));

        var reuse = await service.RefreshAsync(first.RefreshToken, ct);

        reuse.ShouldBeOfType<RefreshOutcome.ReuseDetected>();
        var afterReuse = await service.RefreshAsync(rotated.Tokens.RefreshToken, ct);
        afterReuse.ShouldBeOfType<RefreshOutcome.ReuseDetected>();
        (await NewContext().RefreshTokens.AsNoTracking().AllAsync(token => token.RevokedAt != null, ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_RotateSecurityAndConcurrencyStamps_When_RevokedTokenIsReused()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = NewContext();
        var service = NewService(db);
        var first = await service.IssueAsync(user, null, ct);
        await service.RefreshAsync(first.RefreshToken, ct);
        var before = await NewContext().Users.AsNoTracking().SingleAsync(row => row.Id == user.Id, ct);
        time.Advance(TokenService.RotationGracePeriod + TimeSpan.FromSeconds(1));

        var reuse = await service.RefreshAsync(first.RefreshToken, ct);

        reuse.ShouldBeOfType<RefreshOutcome.ReuseDetected>().UserId.ShouldBe(user.Id);
        var after = await NewContext().Users.AsNoTracking().SingleAsync(row => row.Id == user.Id, ct);
        after.SecurityStamp.ShouldNotBeNullOrWhiteSpace();
        after.SecurityStamp.ShouldNotBe(before.SecurityStamp);
        after.ConcurrencyStamp.ShouldNotBe(before.ConcurrencyStamp);
    }

    [Fact]
    public async Task Should_ReturnInvalid_When_TokenExpiredOrUnknown()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = NewContext();
        var service = NewService(db);
        var issued = await service.IssueAsync(user, null, ct);

        (await service.RefreshAsync("unknown-token", ct)).ShouldBeOfType<RefreshOutcome.Invalid>();

        time.Advance(TimeSpan.FromDays(30) + TimeSpan.FromSeconds(1));
        (await service.RefreshAsync(issued.RefreshToken, ct)).ShouldBeOfType<RefreshOutcome.Invalid>();
    }

    [Fact]
    public async Task Should_LetOnlyOneWin_When_SameTokenIsRefreshedConcurrently()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var seedDb = NewContext();
        var issued = await NewService(seedDb).IssueAsync(user, null, ct);

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var db = NewContext();
            return await NewService(db).RefreshAsync(issued.RefreshToken, ct);
        }));

        var winner = outcomes.OfType<RefreshOutcome.Rotated>().ShouldHaveSingleItem();
        outcomes.Count(outcome => outcome is RefreshOutcome.Superseded).ShouldBe(7);
        await using var db = NewContext();
        (await NewService(db).RefreshAsync(winner.Tokens.RefreshToken, ct)).ShouldBeOfType<RefreshOutcome.Rotated>();
    }

    [Fact]
    public async Task Should_ReturnSupersededAndKeepNewerToken_When_RotatedTokenReplayedWithinGrace()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = NewContext();
        var service = NewService(db);
        var first = await service.IssueAsync(user, null, ct);
        var rotated = (RefreshOutcome.Rotated)await service.RefreshAsync(first.RefreshToken, ct);
        time.Advance(TokenService.RotationGracePeriod - TimeSpan.FromSeconds(1));

        (await service.RefreshAsync(first.RefreshToken, ct)).ShouldBeOfType<RefreshOutcome.Superseded>();

        (await service.RefreshAsync(rotated.Tokens.RefreshToken, ct)).ShouldBeOfType<RefreshOutcome.Rotated>();
    }

    [Fact]
    public async Task Should_RevokeFamilyOnly_When_RevokingByToken()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = NewContext();
        var service = NewService(db);
        var first = await service.IssueAsync(user, null, ct);
        var other = await service.IssueAsync(user, null, ct);

        await service.RevokeFamilyAsync(first.RefreshToken, ct);

        (await service.RefreshAsync(first.RefreshToken, ct)).ShouldBeOfType<RefreshOutcome.ReuseDetected>();
        (await service.RefreshAsync(other.RefreshToken, ct)).ShouldBeOfType<RefreshOutcome.Rotated>();
    }

    [Fact]
    public async Task Should_RevokeEveryFamily_When_RevokingAllForUser()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = NewContext();
        var service = NewService(db);
        await service.IssueAsync(user, null, ct);
        await service.IssueAsync(user, null, ct);

        await service.RevokeAllAsync(user.Id, ct);

        (await NewContext().RefreshTokens.AsNoTracking().AllAsync(token => token.RevokedAt != null, ct)).ShouldBeTrue();
    }

    private AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);

    private TokenService NewService(AppDbContext db) => new(db, Options.Create(Jwt), time);
}

public sealed class TokenBearerCompatibilityTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    [Fact]
    public async Task Should_AuthenticateWithBearer_When_TokenIssuedByTokenService()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = "bearer@example.com",
            NormalizedUserName = "BEARER@EXAMPLE.COM",
            Email = "bearer@example.com",
            NormalizedEmail = "BEARER@EXAMPLE.COM",
            Name = "Bearer",
            SecurityStamp = Guid.NewGuid().ToString("N")
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        var issued = await scope.ServiceProvider.GetRequiredService<ITokenService>().IssueAsync(user, null, ct);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", issued.AccessToken);
        using var response = await client.GetAsync("/v1/system/ping", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}

internal sealed class TestTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset now = start;

    public override DateTimeOffset GetUtcNow() => now;

    public void Advance(TimeSpan by) => now += by;
}
