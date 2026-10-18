using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mita.Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Users;

public sealed class ControlCharacterTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string MePath = "/v1/me";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string> ControlCharacters => new() { "\0", "\u0001", "\u001B", "\u007F" };

    [Theory]
    [MemberData(nameof(ControlCharacters))]
    public async Task Should_ReturnInvalidEmail_When_RegisterEmailHasControlCharacter(string control)
    {
        using var client = new AuthClient(factory);
        var credentials = AuthClient.NewCredentials();

        using var response = await client.RegisterAsync(
            credentials with { Email = credentials.Email.Replace("@", control + "@", StringComparison.Ordinal) },
            ct: Ct);

        (await ErrorsAsync(response)).ShouldBe([("email", "validation.invalidEmail")]);
    }

    [Theory]
    [MemberData(nameof(ControlCharacters))]
    public async Task Should_ReturnInvalidValue_When_RegisterNameHasControlCharacter(string control)
    {
        using var client = new AuthClient(factory);
        var credentials = AuthClient.NewCredentials();

        using var response = await client.RegisterAsync(credentials with { Name = "Anna" + control + "Nowak" }, ct: Ct);

        (await ErrorsAsync(response)).ShouldBe([("name", "validation.invalidValue")]);
    }

    [Theory]
    [MemberData(nameof(ControlCharacters))]
    public async Task Should_ReturnInvalidEmail_When_LoginEmailHasControlCharacter(string control)
    {
        using var registering = new AuthClient(factory);
        var credentials = await registering.RegisterNewAsync(Ct);
        using var client = new AuthClient(factory);

        using var response = await client.LoginAsync(credentials.Email + control, credentials.Password, Ct);

        (await ErrorsAsync(response)).ShouldBe([("email", "validation.invalidEmail")]);
        client.Cookie("mita_at").ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(ControlCharacters))]
    public async Task Should_RejectUpdate_When_NameOrEmailHasControlCharacter(string control)
    {
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);

        using var response = await UpdateAsync(
            client,
            new { name = credentials.Name + control + "x", email = control + credentials.Email });

        (await ErrorsAsync(response)).ShouldBe(
            [("name", "validation.invalidValue"), ("email", "validation.invalidEmail")],
            ignoreOrder: true);
        using var me = await client.Http.GetAsync(MePath, Ct);
        var body = await me.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("name").GetString().ShouldBe(credentials.Name);
        body.GetProperty("email").GetString().ShouldBe(credentials.Email);
    }

    private static async Task<HttpResponseMessage> UpdateAsync(AuthClient client, object body)
    {
        using var request = client.WithXsrf(new HttpRequestMessage(HttpMethod.Put, MePath) { Content = JsonContent.Create(body) });
        return await client.Http.SendAsync(request, Ct);
    }

    private static async Task<(string Field, string Code)[]> ErrorsAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return
        [
            .. problem.GetProperty("errors").EnumerateObject()
                .SelectMany(field => field.Value.EnumerateArray().Select(code => (field.Name, code.GetString()!)))
        ];
    }
}
