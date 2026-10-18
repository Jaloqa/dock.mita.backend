using System.Net;
using System.Net.Http.Json;
using Bogus;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Shouldly;

namespace Mita.Api.IntegrationTests.Infrastructure;

public sealed record TestCredentials(string Name, string Email, string Password);

public sealed class AuthClient : IDisposable
{
    public const string RegisterPath = "/v1/auth/register";
    public const string LoginPath = "/v1/auth/login";
    public const string XsrfCookie = "XSRF-TOKEN";
    public const string XsrfHeader = "X-XSRF-TOKEN";

    public static readonly Uri BaseAddress = new("https://localhost");

    private static readonly Faker Faker = new();

    public AuthClient(WebApplicationFactory<Program> factory)
    {
        Http = factory.CreateDefaultClient(BaseAddress, new CookieContainerHandler(Cookies));
    }

    public CookieContainer Cookies { get; } = new();

    public HttpClient Http { get; }

    public string? Xsrf => Cookie(XsrfCookie)?.Value;

    public static TestCredentials NewCredentials() => new(
        Faker.Name.FirstName(),
        $"{Guid.NewGuid():N}@example.com",
        $"pw-{Guid.NewGuid():N}");

    public Cookie? Cookie(string name, string path = "/") =>
        Cookies.GetCookies(new Uri(BaseAddress, path)).FirstOrDefault(cookie => cookie.Name == name);

    public Task<HttpResponseMessage> RegisterAsync(
        TestCredentials credentials,
        string? acceptLanguage = null,
        CancellationToken ct = default) =>
        RegisterAsync(
            new { name = credentials.Name, email = credentials.Email, password = credentials.Password },
            acceptLanguage,
            ct);

    public Task<HttpResponseMessage> RegisterAsync(object body, string? acceptLanguage = null, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, RegisterPath) { Content = JsonContent.Create(body) };
        if (acceptLanguage is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage);
        }

        return SendAndDisposeAsync(request, ct);
    }

    public async Task<TestCredentials> RegisterNewAsync(CancellationToken ct = default)
    {
        var credentials = NewCredentials();
        using var response = await RegisterAsync(credentials, ct: ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return credentials;
    }

    public Task<HttpResponseMessage> LoginAsync(TestCredentials credentials, CancellationToken ct = default) =>
        LoginAsync(credentials.Email, credentials.Password, ct);

    public Task<HttpResponseMessage> LoginAsync(string? email, string? password, CancellationToken ct = default) =>
        SendAndDisposeAsync(
            new HttpRequestMessage(HttpMethod.Post, LoginPath) { Content = JsonContent.Create(new { email, password }) },
            ct);

    public HttpRequestMessage WithXsrf(HttpRequestMessage request)
    {
        request.Headers.Remove(XsrfHeader);
        request.Headers.Add(XsrfHeader, Xsrf ?? throw new InvalidOperationException("There is no XSRF cookie yet."));
        return request;
    }

    public void Dispose() => Http.Dispose();

    private async Task<HttpResponseMessage> SendAndDisposeAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using (request)
        {
            return await Http.SendAsync(request, ct);
        }
    }
}
