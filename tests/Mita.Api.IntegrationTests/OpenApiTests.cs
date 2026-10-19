using System.Net;
using System.Text.Json;
using Mita.Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Mita.Api.IntegrationTests;

public sealed class OpenApiTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    [Fact]
    public async Task Should_ServeOpenApiDocument()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var info = document.RootElement.GetProperty("info");
        info.GetProperty("title").GetString().ShouldBe("Mita API");
        info.GetProperty("version").GetString().ShouldBe("v1");
    }

    [Fact]
    public async Task Should_ListVersionedPaths_WithoutVersionTemplate()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var paths = document.RootElement.GetProperty("paths");
        paths.TryGetProperty("/v1/system/ping", out _).ShouldBeTrue();
        paths.EnumerateObject().ShouldNotContain(path => path.Name.Contains("{version}"));
    }

    [Fact]
    public async Task Should_DescribeAuthUsersAndSettingsEndpoints()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = document.RootElement;
        var paths = root.GetProperty("paths");
        paths.TryGetProperty("/v1/auth/login", out var login).ShouldBeTrue();
        paths.TryGetProperty("/v1/me", out var me).ShouldBeTrue();
        paths.TryGetProperty("/v1/settings", out var settings).ShouldBeTrue();

        var schemes = root.GetProperty("components").GetProperty("securitySchemes");
        var cookie = schemes.GetProperty("cookie");
        cookie.GetProperty("type").GetString().ShouldBe("apiKey");
        cookie.GetProperty("in").GetString().ShouldBe("cookie");
        cookie.GetProperty("name").GetString().ShouldBe("mita_at");
        var bearer = schemes.GetProperty("bearer");
        bearer.GetProperty("type").GetString().ShouldBe("http");
        bearer.GetProperty("scheme").GetString().ShouldBe("bearer");

        login.GetProperty("post").TryGetProperty("security", out _).ShouldBeFalse();
        me.GetProperty("get").GetProperty("security").GetArrayLength().ShouldBe(2);
        settings.GetProperty("put").GetProperty("security").GetArrayLength().ShouldBe(2);

        TagOf(login.GetProperty("post")).ShouldBe("Auth");
        TagOf(me.GetProperty("get")).ShouldBe("Users");
        TagOf(settings.GetProperty("get")).ShouldBe("Settings");
    }

    [Fact]
    public async Task Should_DescribeAccountsTransfersAndRates()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var paths = document.RootElement.GetProperty("paths");
        paths.TryGetProperty("/v1/accounts", out var accounts).ShouldBeTrue();
        paths.TryGetProperty("/v1/accounts/{id}", out var account).ShouldBeTrue();
        paths.TryGetProperty("/v1/transfers", out var transfers).ShouldBeTrue();
        paths.TryGetProperty("/v1/rates", out var rates).ShouldBeTrue();

        TagOf(accounts.GetProperty("get")).ShouldBe("Accounts");
        TagOf(accounts.GetProperty("post")).ShouldBe("Accounts");
        TagOf(account.GetProperty("put")).ShouldBe("Accounts");
        TagOf(account.GetProperty("delete")).ShouldBe("Accounts");
        TagOf(transfers.GetProperty("get")).ShouldBe("Transfers");
        TagOf(transfers.GetProperty("post")).ShouldBe("Transfers");
        TagOf(rates.GetProperty("get")).ShouldBe("Rates");
    }

    [Fact]
    public async Task Should_DescribeCategoriesAndTransactions()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var paths = document.RootElement.GetProperty("paths");
        paths.TryGetProperty("/v1/categories", out var categories).ShouldBeTrue();
        paths.TryGetProperty("/v1/categories/{id}", out var category).ShouldBeTrue();
        paths.TryGetProperty("/v1/transactions", out var transactions).ShouldBeTrue();
        paths.TryGetProperty("/v1/transactions/{id}", out var transaction).ShouldBeTrue();

        TagOf(categories.GetProperty("get")).ShouldBe("Categories");
        TagOf(categories.GetProperty("post")).ShouldBe("Categories");
        TagOf(category.GetProperty("put")).ShouldBe("Categories");
        TagOf(category.GetProperty("delete")).ShouldBe("Categories");
        TagOf(transactions.GetProperty("get")).ShouldBe("Transactions");
        TagOf(transactions.GetProperty("post")).ShouldBe("Transactions");
        TagOf(transaction.GetProperty("get")).ShouldBe("Transactions");
        TagOf(transaction.GetProperty("put")).ShouldBe("Transactions");
        TagOf(transaction.GetProperty("delete")).ShouldBe("Transactions");
    }

    private static string? TagOf(JsonElement operation) =>
        operation.GetProperty("tags")[0].GetString();

    [Fact]
    public async Task Should_NotServeScalar_OutsideDevelopment()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/scalar", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
