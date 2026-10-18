using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.Middleware;
using Shouldly;

namespace Mita.Api.UnitTests.Middleware;

public sealed class BadRequestExceptionHandlerTests
{
    [Theory]
    [InlineData("Required parameter \"string Search\" was not provided from query string.", "search")]
    [InlineData("Required parameter \"Guid OrderId\" was not provided from route.", "orderId")]
    [InlineData("Failed to bind parameter \"int PageSize\" from \"x\".", "pageSize")]
    [InlineData("Failed to read parameter \"EchoBody body\" from the request body as JSON.", "body")]
    public async Task Should_ReportFieldName_When_ParameterCannotBeResolved(string message, string field)
    {
        await using var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();

        var handled = await new BadRequestExceptionHandler().TryHandleAsync(
            context,
            new BadHttpRequestException(message),
            TestContext.Current.CancellationToken);

        handled.ShouldBeTrue();
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(
            context.Response.Body,
            cancellationToken: TestContext.Current.CancellationToken);
        json.RootElement.GetProperty("errors").EnumerateObject().Select(p => p.Name).ShouldBe([field]);
    }
}
