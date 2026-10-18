using Microsoft.AspNetCore.Mvc;

namespace Mita.Api.Common;

internal sealed class ErrorProblemResult(Error error) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var problem = CreateProblem();
        httpContext.Response.StatusCode = problem.Status!.Value;

        await ProblemDetailsWriter.WriteAsync(httpContext, problem);
    }

    private ProblemDetails CreateProblem()
    {
        switch (error.Type)
        {
            case ErrorType.Validation:
                return new ValidationProblemDetails(error.Fields?.ToDictionary(f => f.Key, f => f.Value) ?? [])
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Validation failed"
                };
            case ErrorType.NotFound:
                return new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Not Found" };
            case ErrorType.Conflict:
                var conflict = new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = "Conflict" };
                conflict.Extensions["code"] = error.Code;
                return conflict;
            case ErrorType.Unauthorized:
                var unauthorized = new ProblemDetails { Status = StatusCodes.Status401Unauthorized, Title = "Unauthorized" };
                unauthorized.Extensions["code"] = error.Code;
                return unauthorized;
            case ErrorType.Forbidden:
                var forbidden = new ProblemDetails { Status = StatusCodes.Status403Forbidden, Title = "Forbidden" };
                forbidden.Extensions["code"] = error.Code;
                return forbidden;
            default:
                throw new ArgumentOutOfRangeException(nameof(error), error.Type, "Unknown error type.");
        }
    }
}
