using Microsoft.AspNetCore.Diagnostics;
using Mita.Api.Common;

namespace Mita.Api.Middleware;

public sealed class BadRequestExceptionHandler : IExceptionHandler
{
    private static readonly Error InvalidBody = Error.Validation(
        new Dictionary<string, string[]> { ["body"] = ["validation.invalidValue"] });

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException { StatusCode: StatusCodes.Status400BadRequest })
        {
            return false;
        }

        await new ErrorProblemResult(InvalidBody).ExecuteAsync(httpContext);
        return true;
    }
}