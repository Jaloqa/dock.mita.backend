using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Mita.Api.IntegrationTests.Infrastructure;

public sealed class LinkClient(WebApplicationFactory<Program> factory) : IDisposable
{
    public const string MemberHeader = "X-Group-Member";

    public HttpClient Http { get; } = factory.CreateDefaultClient(AuthClient.BaseAddress);

    public static string PathOf(string token, string path = "") => $"/v1/shared/groups/{token}{path}";

    public Task<HttpResponseMessage> GetAsync(string token, string path = "") =>
        Http.GetAsync(PathOf(token, path), TestContext.Current.CancellationToken);

    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string token,
        string path,
        object? body = null,
        string? member = null)
    {
        using var request = new HttpRequestMessage(method, PathOf(token, path));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (member is not null)
        {
            request.Headers.TryAddWithoutValidation(MemberHeader, member);
        }

        return await Http.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public void Dispose() => Http.Dispose();
}
