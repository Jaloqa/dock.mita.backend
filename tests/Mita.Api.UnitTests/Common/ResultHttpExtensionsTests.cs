using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.Common;
using Shouldly;

namespace Mita.Api.UnitTests.Common;

public sealed class ResultHttpExtensionsTests
{
    [Fact]
    public async Task Should_MapValidationErrorTo400WithCamelCaseFields()
    {
        Result<string> result = Error.Validation(new Dictionary<string, string[]>
        {
            ["name"] = ["validation.required"],
            ["items[0].name"] = ["validation.tooLong"]
        });

        var (context, json) = await ExecuteAsync(result.ToHttp(value => Results.Ok(value)));

        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        context.Response.ContentType.ShouldStartWith("application/problem+json");
        json.RootElement.GetProperty("title").GetString().ShouldBe("Validation failed");
        var errors = json.RootElement.GetProperty("errors");
        errors.GetProperty("name")[0].GetString().ShouldBe("validation.required");
        errors.GetProperty("items[0].name")[0].GetString().ShouldBe("validation.tooLong");
        json.RootElement.GetProperty("correlationId").GetString().ShouldBe("corr-1");
    }

    [Fact]
    public async Task Should_MapNotFoundTo404()
    {
        Result<string> result = Error.NotFound();

        var (context, json) = await ExecuteAsync(result.ToHttp(value => Results.Ok(value)));

        context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        context.Response.ContentType.ShouldStartWith("application/problem+json");
        json.RootElement.GetProperty("title").GetString().ShouldBe("Not Found");
        json.RootElement.GetProperty("correlationId").GetString().ShouldBe("corr-1");
    }

    [Fact]
    public async Task Should_MapConflictTo409WithCode()
    {
        Result<string> result = Error.Conflict("order.alreadyPaid");

        var (context, json) = await ExecuteAsync(result.ToHttp(value => Results.Ok(value)));

        context.Response.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        json.RootElement.GetProperty("title").GetString().ShouldBe("Conflict");
        json.RootElement.GetProperty("code").GetString().ShouldBe("order.alreadyPaid");
    }

    [Fact]
    public async Task Should_MapUnauthorizedAndForbiddenToTheirStatuses()
    {
        Result unauthorized = Error.Unauthorized();
        Result forbidden = Error.Forbidden();

        var (unauthorizedContext, _) = await ExecuteAsync(unauthorized.ToHttp(() => Results.NoContent()));
        var (forbiddenContext, _) = await ExecuteAsync(forbidden.ToHttp(() => Results.NoContent()));

        unauthorizedContext.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        forbiddenContext.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Should_UseSuccessResult_When_ResultSucceeded()
    {
        Result<string> result = "hello";

        var (context, _) = await ExecuteAsync(result.ToHttp(value => Results.Text(value)), parseBody: false);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
    }

    private static async Task<(DefaultHttpContext Context, JsonDocument Json)> ExecuteAsync(
        IResult result,
        bool parseBody = true)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["correlationId"] = "corr-1");

        var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        context.Response.Body = new MemoryStream();

        await result.ExecuteAsync(context);

        context.Response.Body.Position = 0;
        var json = parseBody
            ? await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken)
            : JsonDocument.Parse("{}");
        return (context, json);
    }
}