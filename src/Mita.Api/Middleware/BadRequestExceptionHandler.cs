using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Diagnostics;
using Mita.Api.Common;

namespace Mita.Api.Middleware;

public sealed partial class BadRequestExceptionHandler : IExceptionHandler
{
    private const string BodyField = "body";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException { StatusCode: StatusCodes.Status400BadRequest } badRequest)
        {
            return false;
        }

        var error = Error.Validation(
            new Dictionary<string, string[]> { [ResolveField(badRequest.Message)] = ["validation.invalidValue"] });
        await new ErrorProblemResult(error).ExecuteAsync(httpContext);
        return true;
    }

    private static string ResolveField(string message)
    {
        var match = ParameterBindingFailure().Match(message);
        return match.Success ? JsonNamingPolicy.CamelCase.ConvertName(match.Groups["name"].Value) : BodyField;
    }

    [GeneratedRegex("""^(?:Failed to bind|Required) parameter "[^"]*? (?<name>[^" ]+)" (?:from |was not provided from )""")]
    private static partial Regex ParameterBindingFailure();
}